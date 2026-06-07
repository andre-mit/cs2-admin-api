using Cs2Admin.API.Models;

namespace Cs2Admin.API.Services.Interfaces;

public interface ISteamTokenService
{
    /// <summary>
    /// Creates a new Steam server token with the given memo and token value, and marks it as available. Returns the ID of the newly created token.
    /// </summary>
    Task<int> CreateTokenAsync(string memo, string token, CancellationToken ct);

    /// <summary>
    /// Marks the token as used (not available) if it is currently available. Returns true if the token was successfully marked as used, false if the token was already used or does not exist.
    /// </summary>
    Task<bool> MarkTokenAsUsedAsync(int tokenId, CancellationToken ct);

    /// <summary>
    /// Marks the token as available if it is currently not available. Returns true if the token was successfully marked as free, false if the token was already free or does not exist.
    /// </summary>
    Task<bool> MarkTokenAsAvailableAsync(int tokenId, CancellationToken ct);

    /// <summary>
    /// Retrieves an available Steam server token from the database. If no tokens are available, it returns null.
    /// </summary>
    Task<SteamServerToken?> GetAvailableTokenAsync(CancellationToken ct);

    /// <summary>
    /// Returns the count of available Steam server tokens (where IsAvailable is true).
    /// </summary>
    Task<int> AvailableTokensCountAsync(CancellationToken ct);
}
