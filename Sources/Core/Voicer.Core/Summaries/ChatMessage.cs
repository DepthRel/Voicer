using Voicer.Core.Enums;

namespace Voicer.Core.Summaries;

/// <summary>
/// A message of a conversation with a language model.
/// </summary>
/// <param name="Role">Who speaks.</param>
/// <param name="Content">What is said.</param>
public sealed record ChatMessage(ChatRole Role, string Content);
