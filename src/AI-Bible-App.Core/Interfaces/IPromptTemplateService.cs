namespace AI_Bible_App.Core.Interfaces
{
    public interface IPromptTemplateService
    {
        string RenderTemplate(string templateKey, IDictionary<string, object?> variables);
        void RegisterTemplate(string key, string template);
    }
}
