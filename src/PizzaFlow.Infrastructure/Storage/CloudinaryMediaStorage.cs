using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Infrastructure.Storage;

public class CloudinaryOptions
{
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string Folder { get; set; } = "pizzaflow";
}

public class CloudinaryMediaStorage(IOptions<CloudinaryOptions> options) : IMediaStorage
{
    private readonly CloudinaryOptions _o = options.Value;
    private Cloudinary Client => new(new Account(_o.CloudName, _o.ApiKey, _o.ApiSecret));

    public async Task<MediaUploadResult> UploadAsync(Stream content, MediaType type, Guid storeId, CancellationToken ct)
    {
        var file = new FileDescription("upload", content);
        var folder = $"{_o.Folder}/{storeId}";
        UploadResult result = type == MediaType.Image
            ? await Client.UploadAsync(new ImageUploadParams { File = file, Folder = folder, UniqueFilename = true, Overwrite = false }, ct)
            : await Client.UploadAsync(new VideoUploadParams { File = file, Folder = folder, UniqueFilename = true, Overwrite = false }, ct);

        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary: {result.Error.Message}");
        return new MediaUploadResult(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId, MediaType type, CancellationToken ct)
    {
        var res = await Client.DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = type == MediaType.Video ? ResourceType.Video : ResourceType.Image
        });
        if (res.Error is not null) throw new InvalidOperationException($"Cloudinary: {res.Error.Message}");
    }
}
