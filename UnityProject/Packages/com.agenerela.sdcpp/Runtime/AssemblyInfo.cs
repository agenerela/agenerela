using System.Runtime.CompilerServices;

// The tests drive SdCppImageProvider with a scripted engine, so they run with no native library
// (DR-028), and the editor's setup window reads the library's state.
[assembly: InternalsVisibleTo("Agenerela.SdCpp.Tests.Editor")]
[assembly: InternalsVisibleTo("Agenerela.SdCpp.Tests.Live")]
[assembly: InternalsVisibleTo("Agenerela.SdCpp.Editor")]
