using Atlas.ActivationContracts.Profiles;

namespace Atlas.Agent.ProfileEngine;

public static class ProfileRestoreEngine
{
    // v1: verify-only (and we keep a place for real restore later)
    public static ProfileVerifyResult Restore(ProfileRestorePayload payload)
    {
        var verify = ProfileVerifyEngine.Verify(payload);
        if (payload.VerifyOnly) return verify;

        if (verify.Status != "verified_ok")
            return verify;

        // Real restore (Phase 2):
        // - decrypt bundle.tar.enc if present
        // - apply files per scope
        // - enforce deny/allow
        // - OS-specific restore hooks
        // For now, we only verify.
        return verify;
    }
}