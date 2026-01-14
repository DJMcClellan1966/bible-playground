using AI_Bible_App.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ILogger<TelemetryService> _logger;

        public TelemetryService(ILogger<TelemetryService> logger)
        {
            _logger = logger;
        }

        public void TrackEvent(string name, IDictionary<string, object?>? properties = null)
        {
            _logger.LogInformation("Event {Name} {@Props}", name, properties);
        }

        public void TrackMetric(string name, double value, IDictionary<string, object?>? properties = null)
        {
            _logger.LogInformation("Metric {Name} = {Value} {@Props}", name, value, properties);
        }
    }
}
