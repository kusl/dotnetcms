namespace MyBlog.Core.Interfaces;

public interface ISlugService
{
    string GenerateSlugOrUuid(string title);

    string GenerateUuidSlug();
}
