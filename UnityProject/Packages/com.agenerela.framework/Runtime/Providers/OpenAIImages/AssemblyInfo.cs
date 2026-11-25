using System.Runtime.CompilerServices;

// The EditMode tests build an OpenAIImagesProvider on a scripted HttpMessageHandler, so they run
// with no server (DR-028). Only they are given access.
[assembly: InternalsVisibleTo("Agenerela.Tests.Editor")]
