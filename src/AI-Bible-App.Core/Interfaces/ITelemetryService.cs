namespace AI_Bible_App.Core.Interfaces
{
    public interface ITelemetryService
    {
        void TrackEvent(string name, IDictionary<string, object?>? properties = null);
        void TrackMetric(string name, double value, IDictionary<string, object?>? properties = null);
    }
}
