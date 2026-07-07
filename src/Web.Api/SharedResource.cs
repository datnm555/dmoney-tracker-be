namespace Web.Api;

/// <summary>
/// Marker type for the shared .resx resources (Resources/SharedResource.en.resx / .vi.resx).
/// Must live in the project ROOT namespace: with ResourcesPath="Resources", the localizer's
/// base name resolves to Web.Api.Resources.SharedResource, matching the compiled resx.
/// </summary>
public sealed class SharedResource;
