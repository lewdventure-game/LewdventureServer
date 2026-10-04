using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Server.Admin.Pages
{
    internal sealed class ErrorModel : PageModel
    {
        public string RequestId { get; private set; } = string.Empty;

        public void OnGet()
        {
            RequestId = Activity.Current == null ? HttpContext.TraceIdentifier : Activity.Current.Id ?? HttpContext.TraceIdentifier;
        }
    }
}
