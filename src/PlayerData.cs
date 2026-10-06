using System.Runtime.CompilerServices;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Trace;

namespace Fixes;

internal readonly struct RampFixTpmCandidate(bool overrode, Vector origin, Vector velocity)
{
    public readonly bool Overrode = overrode;
    public readonly Vector Origin = origin;
    public readonly Vector Velocity = velocity;
}

internal sealed class PlayerData
{
    public volatile bool InGame;

    public int LastJumpTick = -1;

    public ulong VoiceSeed;

    public Vector RampFixLastValidPlaneNormal;
    public bool RampFixDidTpm;
    public RampFixTpmCandidate? RampFixTpmCandidate;

    public TraceParams RampFixTraceParams;
    public CCSPlayerPawn? RampFixTraceParamsPawn;
    public ulong RampFixTraceParamsInteractsWith;
    public ushort RampFixTraceParamsHierarchyId;
}

internal static class PlayerExtensions
{
    private static readonly ConditionalWeakTable<IPlayer, PlayerData> Table = new();

    extension(IPlayer player)
    {
        public PlayerData FixesData => Table.GetValue(player, static _ => new PlayerData());
    }
}
