using System.Runtime.CompilerServices;

// The EditMode tests set a default through the Project Settings page's own code, at a path of
// their choosing (#92). Nothing else is given access.
[assembly: InternalsVisibleTo("Agenerela.Tests.Editor")]
