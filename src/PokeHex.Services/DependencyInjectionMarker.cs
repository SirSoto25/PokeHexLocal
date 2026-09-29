using Microsoft.Extensions.DependencyInjection;

namespace PokeHex.Services;

// Marker so Microsoft.Extensions.DependencyInjection types resolve via package from UI/Desktop hosts.
// Extensions live in EditorFacades.cs / ServiceCollectionExtensions — this file keeps the package reference intentional for classlib consumers.
internal static class DependencyInjectionMarker { }
