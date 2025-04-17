using Microsoft.AspNetCore.Mvc;
using PdfFieldExtractor.Models;
using PdfFieldExtractor.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PdfFieldExtractor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogService _log;

    public PdfController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogService log)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Upload a PDF and ask a semantic question. OpenAI will extract structured data from the document.
    /// </summary>
    /// <param name="request">Request containing the PDF file and the query</param>
    /// <returns>Structured JSON response</returns>
    [HttpPost("ask")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AskQuestion([FromForm] AskPdfRequest request)
    {
        var file = request.File;
        var query = request.Query;

        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("No query provided.");

        string? fileId = null;

        try
        {
            fileId = await UploadFileToOpenAI(file);
            if (fileId == null)
            {
                _log.Error("File upload to OpenAI failed.");
                return StatusCode(500, "File upload to OpenAI failed.");
            }

            string responseContent = await AskOpenAI(fileId, query);

            _log.Info("OpenAI JSON response parsed and returned successfully.");
            return Content(responseContent, "application/json");
        }
        catch (HttpRequestException ex)
        {
            _log.Error("HTTP request to OpenAI failed.", ex);
            return StatusCode(503, "Could not reach OpenAI.");
        }
        catch (Exception ex)
        {
            _log.Error("Unexpected server error.", ex);
            return StatusCode(500, "An unexpected error occurred.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(fileId))
            {
                try
                {
                    await DeleteOpenAIFile(fileId);
                    _log.Info($"Deleted OpenAI file: {fileId}");
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to delete OpenAI file {fileId}. Exception: {ex.Message}");
                }
            }
        }
    }

    private async Task<string?> UploadFileToOpenAI(IFormFile file)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["OpenAI:ApiKey"]);

        using var form = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream();
        form.Add(new StreamContent(fileStream), "file", file.FileName);
        form.Add(new StringContent("assistants"), "purpose");

        var response = await client.PostAsync("https://api.openai.com/v1/files", form);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _log.Error($"Upload failed: {body}");
            return null;
        }

        var doc = JsonDocument.Parse(body);
        var fileId = doc.RootElement.GetProperty("id").GetString();
        _log.Info($"Uploaded file to OpenAI with ID: {fileId}");
        return fileId;
    }

    private async Task<string> AskOpenAI(string fileId, string query)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["OpenAI:ApiKey"]);

        var requestData = new
        {
            model = "gpt-4-turbo",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = $"You are an AI that extracts structured information from PDF documents. Carefully read the attached file and answer the following query:\n\n\"{query}\"\n\nReturn your response as valid JSON with no extra commentary or formatting."
                        },
                        new
                        {
                            type = "file",
                            file = new { file_id = fileId }
                        }
                    }
                }
            },
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(requestData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _log.Error($"OpenAI chat completion error: {result}");
            throw new Exception("OpenAI returned a failure response.");
        }

        var root = JsonDocument.Parse(result).RootElement;
        string gptResponse = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
        _log.Info("Received structured response from GPT.");
        return gptResponse;
    }

    private async Task DeleteOpenAIFile(string fileId)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["OpenAI:ApiKey"]);

        var response = await client.DeleteAsync($"https://api.openai.com/v1/files/{fileId}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _log.Warn($"Failed to delete OpenAI file {fileId}: {body}");
        }
    }
}
