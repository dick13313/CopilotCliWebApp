using Microsoft.AspNetCore.Mvc;
using CopilotApi.Models;
using CopilotApi.Options;
using Microsoft.Extensions.Options;

namespace CopilotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChannelController : ControllerBase
{
    private readonly TelegramOptions _telegramOptions;

    public ChannelController(IOptions<TelegramOptions> telegramOptions)
    {
        _telegramOptions = telegramOptions.Value;
    }

    [HttpGet]
    public ActionResult<List<ChatChannelInfo>> GetChannels()
    {
        var channels = new List<ChatChannelInfo>
        {
            new()
            {
                Name = "telegram",
                Enabled = !string.IsNullOrWhiteSpace(_telegramOptions.BotToken),
                Status = string.IsNullOrWhiteSpace(_telegramOptions.BotToken) ? "disabled" : "running"
            },
            new()
            {
                Name = "discord",
                Enabled = false,
                Status = "not_implemented"
            },
            new()
            {
                Name = "slack",
                Enabled = false,
                Status = "not_implemented"
            },
            new()
            {
                Name = "line",
                Enabled = false,
                Status = "not_implemented"
            }
        };

        return Ok(channels);
    }

    [HttpGet("telegram")]
    public ActionResult<object> GetTelegramSettings()
    {
        return Ok(new
        {
            enabled = !string.IsNullOrWhiteSpace(_telegramOptions.BotToken),
            allowedChatId = _telegramOptions.AllowedChatId,
            defaultModel = _telegramOptions.DefaultModel
        });
    }
}
