namespace Gma.Framework.Api.Serilog;

using Microsoft.AspNetCore.Http;
using global::Serilog;

public interface IRequestLoggingDiagnosticContextContributor
{
    void Enrich(IDiagnosticContext diagnosticContext, HttpContext httpContext);
}
