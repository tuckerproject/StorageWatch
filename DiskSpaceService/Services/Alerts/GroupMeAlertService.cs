using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiskSpaceService.Services.Alerts
{
    public class GroupMeAlertService
    {
        private readonly HttpClient _http = new HttpClient();
        private readonly string _botId;

        public GroupMeAlertService(string botId)
        {
            _botId = botId;
        }

        public async Task SendMessageAsync(string message)
        {
            var payload = new
            {
                bot_id = _botId,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _http.PostAsync("https://api.groupme.com/v3/bots/post", content);
        }
    }
}