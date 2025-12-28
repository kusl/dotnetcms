namespace MyBlog.Core.Models;

/// <summary>
/// Represents an image stored as a BLOB in the database.
/// </summary>
public sealed class Image
{
    /// <summary>Gets or sets the unique identifier for the image.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the original file name.</summary>
    public required string FileName { get; set; }

    /// <summary>Gets or sets the MIME content type.</summary>
    public required string ContentType { get; set; }

    /// <summary>Gets or sets the binary image data.</summary>
    public required byte[] Data { get; set; }

    /// <summary>Gets or sets the associated post ID (optional).</summary>
    public Guid? PostId { get; set; }

    /// <summary>Gets or sets when the image was uploaded.</summary>
    public DateTime UploadedAtUtc { get; set; }

    /// <summary>Gets or sets the ID of the user who uploaded the image.</summary>
    public Guid UploadedByUserId { get; set; }

    /// <summary>Navigation property for the associated post.</summary>
    public Post? Post { get; set; }

    /// <summary>Navigation property for the uploader.</summary>
    public User? UploadedBy { get; set; }
}
