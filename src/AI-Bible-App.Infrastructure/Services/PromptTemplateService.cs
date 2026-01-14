using AI_Bible_App.Core.Interfaces;
using System.Text.RegularExpressions;

namespace AI_Bible_App.Infrastructure.Services
{
    public class PromptTemplateService : IPromptTemplateService
    {
        private readonly Dictionary<string, string> _templates = new();

        public void RegisterTemplate(string key, string template)
        {
            _templates[key] = template;
        }

        public string RenderTemplate(string templateKey, IDictionary<string, object?> variables)
        {
            if (!_templates.TryGetValue(templateKey, out var template))
                throw new KeyNotFoundException(templateKey);

            // Simple {{key}} substitution
            var result = Regex.Replace(template, @"\{\{\s*(\w+)\s*\}\}", match =>
            {
                var name = match.Groups[1].Value;
                if (variables != null && variables.TryGetValue(name, out var val) && val != null)
                    return val.ToString()!;
                return string.Empty;
            });

            return result;
        }
    }
}
