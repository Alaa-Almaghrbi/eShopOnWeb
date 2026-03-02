using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.eShopWeb.PublicApi;

public class CustomSchemaFilters : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema?.Properties == null) return;

        var propertiesToExclude = new[] { "CorrelationId" };

        foreach (var propertyName in propertiesToExclude)
        {
            if (schema.Properties.ContainsKey(propertyName))
            {
                schema.Properties.Remove(propertyName);
            }
        }
    }
}
