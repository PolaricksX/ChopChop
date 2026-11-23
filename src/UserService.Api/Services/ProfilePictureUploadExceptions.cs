namespace UserService.Api.Services;

public sealed class ProfilePictureBadRequestException(string message) : Exception(message);

public sealed class ProfilePicturePayloadTooLargeException(string message) : Exception(message);

public sealed class ProfilePictureUnsupportedMediaTypeException(string message) : Exception(message);
