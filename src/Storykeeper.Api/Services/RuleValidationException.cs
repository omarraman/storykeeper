namespace Storykeeper.Api.Services;

public sealed class RuleValidationException(string message) : Exception(message);

public sealed class RuleConflictException(string message) : Exception(message);
