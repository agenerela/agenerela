using System.Runtime.CompilerServices;

// The EditMode tests build a ComfyUIProvider on a scripted HttpMessageHandler, so they run with no
// server (DR-025). Only they are given access.
[assembly: InternalsVisibleTo("Agenerela.Tests.Editor")]
