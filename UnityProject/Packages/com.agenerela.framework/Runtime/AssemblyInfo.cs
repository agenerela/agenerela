using System.Runtime.CompilerServices;

// The EditMode tests reach the framework's internal seams, such as the HttpMessageHandler an
// OllamaProvider can be built with, so they run with no server installed (#88). Nothing else
// is given access.
[assembly: InternalsVisibleTo("Agenerela.Tests.Editor")]
