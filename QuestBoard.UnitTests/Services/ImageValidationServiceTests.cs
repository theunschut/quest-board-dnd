using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

public class ImageValidationServiceTests
{
    private const long OneMegabyte = 1024 * 1024;
    private const long MaxFileSizeBytes = 5 * OneMegabyte;

    private static readonly IImageValidationService Service = new ImageValidationService();

    [Theory]
    [InlineData("image/jpeg", "photo.jpg")]
    [InlineData("image/jpeg", "photo.jpeg")]
    [InlineData("image/png", "photo.png")]
    [InlineData("image/gif", "photo.gif")]
    public void ValidateImagePair_ValidOriginalWithinSizeLimit_ReturnsNoErrors(string contentType, string fileName)
    {
        var original = new ImageFileInput(OneMegabyte, contentType, fileName, "ProfilePictureFile");

        var errors = Service.ValidateImagePair(original, null);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateImagePair_WrongMimeType_ReturnsOneErrorOnThatField()
    {
        var original = new ImageFileInput(OneMegabyte, "application/pdf", "document.pdf", "ProfilePictureFile");

        var errors = Service.ValidateImagePair(original, null);

        errors.Should().ContainSingle(e => e.FieldName == "ProfilePictureFile");
    }

    [Theory]
    [InlineData(".bmp")]
    [InlineData(".svg")]
    public void ValidateImagePair_WrongExtension_ReturnsOneErrorOnThatField(string extension)
    {
        var original = new ImageFileInput(OneMegabyte, "image/jpeg", $"photo{extension}", "ProfilePictureFile");

        var errors = Service.ValidateImagePair(original, null);

        errors.Should().ContainSingle(e => e.FieldName == "ProfilePictureFile");
    }

    [Fact]
    public void ValidateImagePair_OverSizeLimit_ReturnsOneSizeErrorOnThatField()
    {
        var original = new ImageFileInput(MaxFileSizeBytes + 1, "image/jpeg", "photo.jpg", "ProfilePictureFile");

        var errors = Service.ValidateImagePair(original, null);

        errors.Should().ContainSingle(e => e.FieldName == "ProfilePictureFile");
    }

    [Fact]
    public void ValidateImagePair_NullOriginalAndNullCropped_ReturnsNoErrors()
    {
        var errors = Service.ValidateImagePair(null, null);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateImagePair_ValidOriginalWithNullCropped_ReturnsNoErrors()
    {
        var original = new ImageFileInput(OneMegabyte, "image/jpeg", "photo.jpg", "ProfilePictureFile");

        var errors = Service.ValidateImagePair(original, null);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateImagePair_ValidOriginalWithInvalidCropped_ReturnsExactlyOneErrorOnCroppedField()
    {
        var original = new ImageFileInput(OneMegabyte, "image/jpeg", "photo.jpg", "ProfilePictureFile");
        var cropped = new ImageFileInput(OneMegabyte, "application/pdf", "crop.pdf", "CroppedImageFile");

        var errors = Service.ValidateImagePair(original, cropped);

        errors.Should().ContainSingle();
        errors.Single().FieldName.Should().Be("CroppedImageFile");
    }

    [Fact]
    public void ValidateImagePair_ZeroLengthFile_TreatedAsAbsent_ReturnsNoErrors()
    {
        var original = new ImageFileInput(0, "application/pdf", "not-really-a-file.pdf", "ProfilePictureFile");

        var errors = Service.ValidateImagePair(original, null);

        errors.Should().BeEmpty();
    }
}
