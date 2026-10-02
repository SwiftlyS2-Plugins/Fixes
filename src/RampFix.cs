using System.Collections.Concurrent;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Trace;

namespace Fixes;

// Ported from zer0.k's RampFix (originally written for ModSharp by Nukoooo) to SwiftlyS2.

public partial class Fixes
{
    private bool rampFixEnabled;

    private const float RampFixBugThreshold = 0.98f;
    private const float RampFixGroundPlaneZ = 0.7f;
    private const float RampFixStraightWallZ = 0.03125f;
    private const float RampFixBugVelocityThreshold = 0.95f;
    private const float RampFixPierceDistance = 0.15f;
    private const int RampFixPierceSteps = 10;
    private const float RampFixNewRampThreshold = 0.95f;
    private const float RampFixFltEpsilon = 1.19209e-07f;
    private const float RampFixUnitPlaneLengthSq = 0.99f * 0.99f;
    private const float RampFixMinMoveDistance = 0.03125f;
    private const float RampFixMinMoveDistanceSq = RampFixMinMoveDistance * RampFixMinMoveDistance;

    private static readonly Vector RampFixEmptyVector = new();
    private Vector[] rampFixOffsetDirections = [];

    // Per-player state, keyed by player slot - mirrors the original's PlayerSlot-indexed arrays.
    private readonly ConcurrentDictionary<int, Vector> _rampFixLastValidPlaneNormal = new();
    private readonly ConcurrentDictionary<int, bool> _rampFixDidTpm = new();
    private readonly ConcurrentDictionary<int, RampFixTpmCandidate> _rampFixTpmCandidate = new();

    private readonly struct RampFixTpmCandidate(bool overrode, Vector origin, Vector velocity)
    {
        public readonly bool Overrode = overrode;
        public readonly Vector Origin = origin;
        public readonly Vector Velocity = velocity;
    }

    private static Vector[] BuildRampFixOffsetDirections()
    {
        ReadOnlySpan<float> offsets = [0.0f, -1.0f, 1.0f];

        var dirs = new Vector[27];

        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                for (var k = 0; k < 3; k++)
                {
                    dirs[(i * 9) + (j * 3) + k] = new Vector(offsets[i], offsets[j], offsets[k]).Normalized();
                }
            }
        }

        return dirs;
    }

    private void SetRampFixEnabled(bool enabled)
    {
        if (enabled == rampFixEnabled)
        {
            return;
        }

        rampFixEnabled = enabled;

        if (enabled)
        {
            EnableRampFix();
            return;
        }

        DisableRampFix();
    }

    private void EnableRampFix()
    {
        if (rampFixOffsetDirections.Length == 0)
        {
            rampFixOffsetDirections = BuildRampFixOffsetDirections();
        }

        Core.GameHooks.Movement.ProcessMovement.Pre += OnRampFixProcessMovementPre;
        Core.GameHooks.Movement.ProcessMovement.Post += OnRampFixProcessMovementPost;
        Core.GameHooks.Movement.TryPlayerMove.Pre += OnRampFixTryPlayerMovePre;
        Core.GameHooks.Movement.TryPlayerMove.Post += OnRampFixTryPlayerMovePost;
        Core.GameHooks.Movement.CategorizePosition.Pre += OnRampFixCategorizePositionPre;
        Core.Event.OnClientDisconnected += OnRampFixClientDisconnected;
    }

    private void DisableRampFix()
    {
        Core.GameHooks.Movement.ProcessMovement.Pre -= OnRampFixProcessMovementPre;
        Core.GameHooks.Movement.ProcessMovement.Post -= OnRampFixProcessMovementPost;
        Core.GameHooks.Movement.TryPlayerMove.Pre -= OnRampFixTryPlayerMovePre;
        Core.GameHooks.Movement.TryPlayerMove.Post -= OnRampFixTryPlayerMovePost;
        Core.GameHooks.Movement.CategorizePosition.Pre -= OnRampFixCategorizePositionPre;
        Core.Event.OnClientDisconnected -= OnRampFixClientDisconnected;
        _rampFixLastValidPlaneNormal.Clear();
        _rampFixDidTpm.Clear();
        _rampFixTpmCandidate.Clear();
    }

    public void OnRampFixClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _rampFixLastValidPlaneNormal.TryRemove(@event.PlayerId, out _);
        _rampFixDidTpm.TryRemove(@event.PlayerId, out _);
        _rampFixTpmCandidate.TryRemove(@event.PlayerId, out _);
    }

    // Tracks, per tick, whether TryPlayerMove actually ran for this player - if it didn't
    // (dead, invalid pawn, etc.) the tracked ramp-plane state is stale and gets cleared.
    private void OnRampFixProcessMovementPre(ref ProcessMovementMovementPreContext ctx)
    {
        _rampFixDidTpm[ctx.Params.Player.Slot] = false;
    }

    private void OnRampFixProcessMovementPost(ref ProcessMovementMovementPostContext ctx)
    {
        var slot = ctx.Params.Player.Slot;

        if (!_rampFixDidTpm.TryGetValue(slot, out var didTpm) || !didTpm)
        {
            _rampFixLastValidPlaneNormal[slot] = RampFixEmptyVector;
        }
    }

    private void OnRampFixTryPlayerMovePre(ref TryPlayerMoveMovementPreContext ctx)
    {
        var player = ctx.Params.Player;
        var pawn = player.PlayerPawn;

        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte) LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        var slot = player.Slot;
        var mv = ctx.Params.MoveData;

        _rampFixDidTpm[slot] = true;

        if (mv.Velocity == Vector.Zero)
        {
            return;
        }

        if (!pawn.GroundEntity.IsValid)
        {
            var overrode = RampFixPreTryPlayerMove(pawn,
                                                   slot,
                                                   mv,
                                                   ctx.Params.FirstDest,
                                                   ctx.Params.FirstTrace,
                                                   out var tpmOrigin,
                                                   out var tpmVelocity);

            _rampFixTpmCandidate[slot] = new RampFixTpmCandidate(overrode, tpmOrigin, tpmVelocity);
        }
        else
        {
            _rampFixLastValidPlaneNormal[slot] = RampFixEmptyVector;
        }
    }

    private void OnRampFixTryPlayerMovePost(ref TryPlayerMoveMovementPostContext ctx)
    {
        var slot = ctx.Params.Player.Slot;

        if (!_rampFixTpmCandidate.TryRemove(slot, out var candidate) || !candidate.Overrode)
        {
            return;
        }

        RampFixPostTryPlayerMove(ctx.Params.MoveData, candidate.Origin, candidate.Velocity);
    }

    private static void RampFixPostTryPlayerMove(IMoveData mv, Vector tpmOrigin, Vector tpmVelocity)
    {
        if (tpmOrigin == RampFixEmptyVector || tpmVelocity == RampFixEmptyVector)
        {
            return;
        }

        var tpmLenSq = tpmVelocity.LengthSquared();
        var mvLenSq  = mv.Velocity.LengthSquared();
        var denomSq  = tpmLenSq * mvLenSq;

        var cos = denomSq > 1e-24f
            ? tpmVelocity.Dot(mv.Velocity) / MathF.Sqrt(denomSq)
            : 0.0f;

        var velocityHeavilyModified =
            cos < RampFixBugThreshold
            || tpmLenSq > 50.0f                           * 50.0f
            && mvLenSq  < RampFixBugVelocityThreshold * RampFixBugVelocityThreshold * tpmLenSq;

        if (velocityHeavilyModified)
        {
            mv.AbsOrigin = tpmOrigin;
            mv.Velocity  = tpmVelocity;
        }
    }

    private void OnRampFixCategorizePositionPre(ref CategorizePositionMovementPreContext ctx)
    {
        var p = ctx.Params;

        if (p.StayOnGround || p.MoveData.Velocity.Z > -64.0f)
        {
            return;
        }

        var pawn = p.Player.PlayerPawn;

        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte) LifeState_t.LIFE_ALIVE)
        {
            return;
        }

        var slot = p.Player.Slot;

        if (!_rampFixLastValidPlaneNormal.TryGetValue(slot, out var lastN)
            || lastN == RampFixEmptyVector
            || lastN.Z > RampFixGroundPlaneZ)
        {
            return;
        }

        var mv     = p.MoveData;
        var bbox   = RampFixGetBBox(pawn);
        var params_ = RampFixBuildTraceParams(pawn);

        var origin       = mv.AbsOrigin;
        var groundOrigin = origin;
        groundOrigin.Z -= 2.0f;

        var trace = Core.Trace.TracePlayerBBox(origin, groundOrigin, bbox, params_);

        if (MathF.Abs(trace.Fraction - 1.0f) < RampFixFltEpsilon)
        {
            return;
        }

        if (trace.Fraction                                   < 0.95f
            && trace.HitNormal.Z                             > RampFixGroundPlaneZ
            && lastN.Dot(trace.HitNormal.Normalized()) < RampFixBugThreshold)
        {
            origin         += lastN * 0.0625f;
            groundOrigin   =  origin;
            groundOrigin.Z -= 2.0f;

            trace = Core.Trace.TracePlayerBBox(origin, groundOrigin, bbox, params_);

            if (trace.StartInSolid)
            {
                return;
            }

            if (MathF.Abs(trace.Fraction - 1.0f)                       < RampFixFltEpsilon
                || lastN.Dot(trace.HitNormal.Normalized()) >= RampFixBugThreshold)
            {
                mv.AbsOrigin = origin;
            }
        }
    }

    private static BBox_t RampFixGetBBox(CCSPlayerPawn pawn)
    {
        var ducked = pawn.MovementServices?.Ducked ?? false;

        return new BBox_t
        {
            Mins = new Vector(-16, -16, 0),
            Maxs = new Vector(16, 16, ducked ? 54.0f : 72.0f),
        };
    }

    // Matches the collision-group/interaction-mask combination the original code built via
    // Sharp.Shared's RnQueryShapeAttr.PlayerMovement(...): CollisionGroup.PlayerMovement,
    // the pawn's own InteractsWith mask and hierarchy id, and the pawn itself ignored.
    private static TraceParams RampFixBuildTraceParams(CCSPlayerPawn pawn)
    {
        var attribute = pawn.Collision!.CollisionAttribute;

        return TraceParams.Builder()
                          .WithIterateEntities(true)
                          .WithCollisionGroup(CollisionGroup.PlayerMovement)
                          .WithInteraction((MaskTrace) attribute.InteractsWith)
                          .WithHierarchyIds(attribute.HierarchyId)
                          .IgnoreEntity(pawn)
                          .Build();
    }

    private static bool RampFixIsTraceBasicallyValid(in TraceResult trace)
    {
        if (trace.StartInSolid)
        {
            return false;
        }

        if (trace.Fraction                     < 1.0f
            && MathF.Abs(trace.HitNormal.X) < RampFixFltEpsilon
            && MathF.Abs(trace.HitNormal.Y) < RampFixFltEpsilon
            && MathF.Abs(trace.HitNormal.Z) < RampFixFltEpsilon)
        {
            return false;
        }

        if (MathF.Abs(trace.HitNormal.X)    > 1.0f
            || MathF.Abs(trace.HitNormal.Y) > 1.0f
            || MathF.Abs(trace.HitNormal.Z) > 1.0f)
        {
            return false;
        }

        return true;
    }

    // The expensive half: two extra traces verifying the end position isn't stuck.
    private bool RampFixVerifyTraceEndNotStuck(in TraceResult trace, in BBox_t bbox, in TraceParams traceParams)
    {
        var stuck = Core.Trace.TracePlayerBBox(trace.EndPos, trace.EndPos, bbox, traceParams);

        if (stuck.StartInSolid || stuck.Fraction < 1.0f - RampFixFltEpsilon)
        {
            return false;
        }

        stuck = Core.Trace.TracePlayerBBox(trace.EndPos, trace.StartPos, bbox, traceParams);

        return !stuck.StartInSolid;
    }

    private bool RampFixIsValidMovementTrace(in TraceResult trace, in BBox_t bbox, in TraceParams traceParams)
        => RampFixIsTraceBasicallyValid(trace) && RampFixVerifyTraceEndNotStuck(trace, bbox, traceParams);

    // -1 = not traced yet, 0 = stuck, 1 = clear. Lets the caller put the two verification
    // traces last in a || chain without ever paying for them twice.
    private bool RampFixIsTraceEndVerified(in TraceResult trace, in BBox_t bbox, in TraceParams traceParams, ref int cached)
    {
        if (cached < 0)
        {
            cached = RampFixVerifyTraceEndNotStuck(trace, bbox, traceParams) ? 1 : 0;
        }

        return cached != 0;
    }

    private static Vector RampFixClipVelocity(in Vector @in, in Vector normal)
    {
        var backoff = -((@in.X * normal.X) + ((normal.Z * @in.Z) + (@in.Y * normal.Y)));

        backoff = MathF.Max(backoff, 0.0f) + 0.03125f;

        return (normal * backoff) + @in;
    }

    // Re-runs the engine's own bump/clip sweep with extra "pierce" probes whenever the
    // engine's own trace for a bump looks suspicious, to find the ramp surface a straight
    // sweep would have clipped through. Returns true if it found a materially different
    // result; the caller (OnRampFixTryPlayerMovePre/Post) decides whether to actually apply
    // it once the real engine call has also run for this tick.
    //
    // NOTE: TraceResult's Fraction/EndPos/HitNormal setters are internal to SwiftlyS2, so
    // unlike the original (which mutated a single CGameTrace* in place), this tracks the
    // "current best trace" as three local variables - pmFraction/pmEndPosition/
    // pmPlaneNormal - that get overwritten by hand at the same points the original
    // overwrote pm->Fraction/pm->EndPosition/pm->PlaneNormal.
    private bool RampFixPreTryPlayerMove(CCSPlayerPawn pawn,
                                         int            slot,
                                         IMoveData      mv,
                                         Vector         firstDest,
                                         TraceResult    firstTrace,
                                         out Vector     tpmOrigin,
                                         out Vector     tpmVelocity)
    {
        var frameTime = Core.Engine.GlobalVars.FrameTime;
        var timeLeft  = frameTime;
        var start     = mv.AbsOrigin;

        var allFraction = 0.0f;

        var velocity       = mv.Velocity;
        var primalVelocity = velocity;

        var potentiallyStuck = false;

        var bbox        = RampFixGetBBox(pawn);
        var traceParams = RampFixBuildTraceParams(pawn);

        var numPlanes = 0;
        var planes    = new Vector[5];

        var lastPlane = _rampFixLastValidPlaneNormal.GetValueOrDefault(slot, RampFixEmptyVector);

        // The hook rejects grounded pawns before entering this method, and we do not
        // re-enter engine movement code while simulating.
        var isWalkingInAir = pawn.MoveType == MoveType_t.MOVETYPE_WALK;

        var overrodeTpm = false;

        var offsetDirections = rampFixOffsetDirections;

        // Mirrors pm->Fraction / pm->EndPosition / pm->PlaneNormal from the original -
        // declared outside the loop so the last bump's values survive to the final
        // tpmOrigin/tpmVelocity assignment below.
        var pmFraction     = 0.0f;
        var pmEndPosition  = start;
        var pmPlaneNormal  = RampFixEmptyVector;

        for (var bumpCount = 0u; bumpCount < 4; bumpCount++)
        {
            var end = start + (velocity * timeLeft);

            Vector pmN;

            if (end == firstDest)
            {
                pmPlaneNormal = firstTrace.HitNormal;
                pmFraction    = firstTrace.Fraction;
                pmEndPosition = firstTrace.EndPos;
                pmN           = pmPlaneNormal.Normalized();
            }
            else
            {
                var pmTrace = Core.Trace.TracePlayerBBox(start, end, bbox, traceParams);

                pmPlaneNormal = pmTrace.HitNormal;
                pmFraction    = pmTrace.Fraction;
                pmEndPosition = pmTrace.EndPos;

                if (start == end)
                {
                    // Nothing left to sweep. start/velocity/timeLeft can no longer change,
                    // so every remaining bump would re-run this exact degenerate trace and
                    // land on the same result - break out instead of burning three more
                    // traces on it.
                    break;
                }

                var basicValid = RampFixIsTraceBasicallyValid(pmTrace);
                var verified   = -1;

                if (basicValid && MathF.Abs(pmFraction - 1.0f) < RampFixFltEpsilon)
                {
                    verified = RampFixVerifyTraceEndNotStuck(pmTrace, bbox, traceParams) ? 1 : 0;

                    if (verified == 1)
                    {
                        break;
                    }
                }

                var lastN = lastPlane; // already unit-length, no sqrt
                pmN = pmPlaneNormal.Normalized();

                var normalChanged = pmN.Dot(lastN) < RampFixBugThreshold;
                var stuck         = potentiallyStuck && pmFraction == 0.0f;

                // A vertical wall is never the ramp we are trying to dodge.
                var lastValidPlaneWasStraightWall = lastN.Z < RampFixStraightWallZ;

                // (normalChanged && !straightWall) || stuck is the reference trigger; an
                // invalid trace counts as a candidate too. The verification traces are the
                // expensive half of IsValidMovementTrace, so they go last in the chain -
                // every cheaper term that short-circuits first now skips them entirely,
                // with the same accept/reject result.
                if (lastN != RampFixEmptyVector
                    && (normalChanged && !lastValidPlaneWasStraightWall
                        || stuck
                        || !basicValid
                        || !RampFixIsTraceEndVerified(pmTrace, bbox, traceParams, ref verified)))
                {
                    for (var d = 0; d < offsetDirections.Length; d++)
                    {
                        Vector offsetDirection;

                        if (d == 0)
                        {
                            offsetDirection = lastN;
                        }
                        else
                        {
                            offsetDirection = offsetDirections[d]; // precomputed unit vector

                            if (lastN.Dot(offsetDirection) <= 0.0f)
                            {
                                continue;
                            }

                            var testStart = start + (offsetDirection * RampFixPierceDistance);
                            var probe      = Core.Trace.TracePlayerBBox(testStart, start, bbox, traceParams);

                            if (!RampFixIsValidMovementTrace(probe, bbox, traceParams))
                            {
                                continue;
                            }
                        }

                        var goodTrace   = false;
                        var hitNewPlane = false;

                        var pierce = default(TraceResult);

                        for (var step = 1; step <= RampFixPierceSteps; step++)
                        {
                            var ratio        = step * (1.0f / RampFixPierceSteps);
                            var pierceOffset = offsetDirection * (ratio * RampFixPierceDistance);
                            var ratioStart   = start + pierceOffset;
                            var ratioEnd     = end   + pierceOffset;

                            pierce = Core.Trace.TracePlayerBBox(ratioStart, ratioEnd, bbox, traceParams);

                            // Cheap, trace-free checks first...
                            if (!RampFixIsTraceBasicallyValid(pierce))
                            {
                                continue;
                            }

                            var pierceFraction = pierce.Fraction;

                            if (MathF.Abs(pierceFraction - 1.0f) < RampFixFltEpsilon * 4.0f)
                            {
                                if (RampFixVerifyTraceEndNotStuck(pierce, bbox, traceParams))
                                {
                                    goodTrace = true;

                                    break;
                                }

                                continue;
                            }

                            var pierceN      = pierce.HitNormal.Normalized();
                            var lastPlaneDot = pierceN.Dot(lastN);

                            var validPlane = pierceFraction < 1.0f
                                             && pierceFraction > 0.1f
                                             && lastPlaneDot  >= RampFixBugThreshold;

                            var wouldHitNewPlane = lastPlaneDot       > RampFixNewRampThreshold
                                                   && pmN.Dot(pierceN) < RampFixNewRampThreshold;

                            var wouldBeGood = validPlane;

                            // ...then pay for the two verification traces only when this
                            // iteration's outcome can actually change anything. If it can't
                            // break the loop (wouldBeGood == false) and wouldn't change the
                            // tracked hitNewPlane flag, then whether the verification passes
                            // (flag set to the same value) or fails (flag left untouched) the
                            // result is identical - so the traces are pure waste. This keeps
                            // accept/reject behaviour bit-identical to the original.
                            if (!wouldBeGood && wouldHitNewPlane == hitNewPlane)
                            {
                                continue;
                            }

                            if (!RampFixVerifyTraceEndNotStuck(pierce, bbox, traceParams))
                            {
                                continue;
                            }

                            hitNewPlane = wouldHitNewPlane;
                            goodTrace   = wouldBeGood;

                            if (goodTrace)
                            {
                                break;
                            }
                        }

                        if (goodTrace || hitNewPlane)
                        {
                            var confirm = Core.Trace.TracePlayerBBox(pierce.EndPos, end, bbox, traceParams);

                            if (!RampFixIsValidMovementTrace(confirm, bbox, traceParams))
                            {
                                continue;
                            }

                            var denomSq = (end - start).LengthSquared();

                            if (denomSq > 1e-12f)
                            {
                                var deltaSq = (pierce.EndPos - pierce.StartPos).LengthSquared();

                                // sqrt(a)/sqrt(b) == sqrt(a/b): one sqrt instead of two
                                pmFraction = Math.Clamp(MathF.Sqrt(deltaSq / denomSq), 0.0f, 1.0f);
                            }
                            else
                            {
                                pmFraction = 0.0f;
                            }

                            pmEndPosition = confirm.EndPos;

                            if (pierce.HitNormal.LengthSquared() > 0.0f)
                            {
                                pmPlaneNormal = pierce.HitNormal;
                                lastPlane     = pierce.HitNormal.Normalized();
                            }
                            else
                            {
                                pmPlaneNormal = confirm.HitNormal;
                                lastPlane     = confirm.HitNormal.Normalized();
                            }

                            overrodeTpm = true;

                            // pmPlaneNormal was just replaced; lastPlane already holds its unit form.
                            pmN = lastPlane;

                            break;
                        }
                    }
                }

                // Gate on the RAW normal being near unit-length: a shorter one is a
                // deformed plane we must not start tracking. (Length() > 0.99f, squared to
                // avoid the sqrt.)
                if (pmPlaneNormal.LengthSquared() > RampFixUnitPlaneLengthSq)
                {
                    lastPlane = pmN;
                }

                potentiallyStuck = pmFraction == 0.0f;
            }

            var fraction = pmFraction;

            // original: fraction * |velocity| > 0.03125 - squared to avoid the sqrt
            if (fraction * fraction * velocity.LengthSquared() > RampFixMinMoveDistanceSq || fraction > RampFixMinMoveDistance)
            {
                allFraction += fraction;
                start       =  pmEndPosition;
                numPlanes   =  0;
            }

            if (MathF.Abs(allFraction - 1.0f) < RampFixFltEpsilon)
            {
                break;
            }

            timeLeft -= frameTime * pmFraction;

            if (numPlanes >= 5 || (pmPlaneNormal.Z >= 0.7f && velocity.Length2D() < 1.0f))
            {
                velocity = RampFixEmptyVector;

                break;
            }

            planes[numPlanes] = pmN;
            numPlanes++;

            if (numPlanes == 1 && isWalkingInAir)
            {
                velocity = RampFixClipVelocity(velocity, planes[0]);
            }
            else
            {
                int i;

                for (i = 0; i < numPlanes; i++)
                {
                    velocity = RampFixClipVelocity(velocity, planes[i]);

                    int j;

                    for (j = 0; j < numPlanes; j++)
                    {
                        if (j == i)
                        {
                            continue;
                        }

                        // Are we now moving against this plane?
                        if (velocity.Dot(planes[j]) < 0)
                        {
                            break; // not ok
                        }
                    }

                    if (j == numPlanes) // Didn't have to clip, so we're ok
                    {
                        break;
                    }
                }

                // Did we go all the way through plane set
                if (i != numPlanes)
                {
                    // go along this plane
                    // velocity is set by the clipping call above, no need to set again.
                }
                else
                {
                    // go along the crease
                    if (numPlanes != 2)
                    {
                        velocity = RampFixEmptyVector;

                        break;
                    }

                    var dir = planes[0].Cross(planes[1]).Normalized();
                    velocity = dir * dir.Dot(velocity);

                    if (velocity.Dot(primalVelocity) <= 0)
                    {
                        velocity = RampFixEmptyVector;

                        break;
                    }
                }
            }
        }

        _rampFixLastValidPlaneNormal[slot] = lastPlane;

        tpmOrigin   = pmEndPosition;
        tpmVelocity = velocity;

        return overrodeTpm;
    }
}
