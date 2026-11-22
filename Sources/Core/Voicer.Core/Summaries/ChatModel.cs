using Voicer.Core.Models;

namespace Voicer.Core.Summaries;

/// <summary>
/// A light language model the application downloads and runs itself for summaries and answers, when no server of
/// language models is used: weaker than a full model, but private and free of a server.
/// </summary>
/// <param name="Id">The identifier: the name of its folder in <c>Data/Models</c> and of the model in the requests.</param>
/// <param name="Name">The name shown to the user.</param>
/// <param name="FileName">The name of the file of the weights.</param>
/// <param name="DownloadUri">Where the file is downloaded from, over HTTPS.</param>
/// <param name="SizeBytes">The exact size of the file.</param>
/// <param name="Sha256">The SHA-256 of the file, in lower-case hex.</param>
/// <param name="ContextTokens">The context the model is loaded with, in tokens.</param>
/// <param name="Thinks">Whether the model reasons aloud before it answers unless told not to.</param>
public sealed record ChatModel(
    string Id,
    string Name,
    string FileName,
    Uri DownloadUri,
    long SizeBytes,
    string Sha256,
    int ContextTokens,
    bool Thinks) : IServerModel
{
    /// <inheritdoc />
    public string DescriptionKey => $"ChatModel.{Id}.Description";
}
