using System.Runtime.CompilerServices;

// The EditMode tests reach the framework's internal seams, such as the HttpMessageHandler an
// OllamaProvider can be built with, so they run with no server installed (#88). Only they and
// the package's Editor assembly, below, are given access.
[assembly: InternalsVisibleTo("Agenerela.Tests.Editor")]

// The package's own Editor tools, such as the Decision Log (#172), read internals such as
// AgentActionReader rather than copy them (#166). They ship in this package. Anything a
// developer's own tool would want stays public.
[assembly: InternalsVisibleTo("Agenerela.Editor")]
