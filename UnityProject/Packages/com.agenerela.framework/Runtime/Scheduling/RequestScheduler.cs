using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The queue in front of one provider instance (build plan §2.6). Every <see cref="Agent"/>
    /// given that instance sends through it, so at most <see cref="MaxInFlight"/> of their requests
    /// are with the provider at once, player-facing before background, first come, first served
    /// within each (DR-015).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One per provider instance, because how many requests a backend takes at once is a property
    /// of the backend, not of an agent (DR-013). <see cref="For"/> finds it by the instance itself,
    /// held weakly: agents given the same instance share one queue, agents on different instances
    /// never wait on each other, and the scheduler goes when its provider does. Nothing has to be
    /// registered, and a provider written by a developer gets a queue like any other.
    /// </para>
    /// <para>
    /// Internal for now. The only setting is <see cref="MaxInFlight"/>, which a provider config
    /// asset sets from its own <see cref="ProviderConfig.MaxInFlight"/> when it builds the provider
    /// (#92). A provider built in code keeps 1, as the prototype measured safe (5 simultaneous
    /// requests, 0 cross-talk; about 21 s for 30 agents, Appendix A).
    /// </para>
    /// <para>
    /// Jumping the queue is all "player-facing goes first" means: a request already sent is never
    /// aborted to make room. A request cancelled while queued leaves the queue at once and never
    /// reaches the provider; once sent, the caller's token is the provider's to honour. Nothing is
    /// retried. Plain C# with no scene, like the rest of <c>Runtime/</c> outside <c>Runtime/Unity/</c>.
    /// </para>
    /// <para>
    /// Extension points, not now: aborting an in-flight background request for a player-facing
    /// one, more than two tiers, and the session budget and per-minute pacing a cloud provider
    /// needs (<see cref="ProviderCapabilities.RateLimit"/>, Phase 6) all go here.
    /// </para>
    /// </remarks>
    internal sealed class RequestScheduler
    {
        private static readonly ConditionalWeakTable<ILLMProvider, RequestScheduler> schedulers =
            new ConditionalWeakTable<ILLMProvider, RequestScheduler>();

        // Guards everything below, and each ticket's state. A cancellation callback can run on any
        // thread, so even a scheduler used only from the main thread is locked.
        private readonly object gate = new object();
        private readonly LinkedList<Ticket> playerFacing = new LinkedList<Ticket>();
        private readonly LinkedList<Ticket> background = new LinkedList<Ticket>();
        private int maxInFlight = 1;
        private int inFlight;

        // True while one call is handing out free slots. Making a ticket ready can run the waiting
        // decision on the spot, which sends, finishes and frees its slot again inside that call;
        // the flag turns that nested call into one more turn of the outer loop instead of a
        // recursion as deep as the queue.
        private bool granting;

        /// <summary>The scheduler every agent given <paramref name="provider"/> sends through.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
        internal static RequestScheduler For(ILLMProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            // Keyed by the instance, never by Equals: two providers that compare equal are still
            // two backends' worth of requests.
            return schedulers.GetValue(provider, _ => new RequestScheduler());
        }

        /// <summary>
        /// How many requests may be with the provider at once. 1 by default; raising it sends
        /// waiting requests at once, lowering it lets those in flight finish.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
        internal int MaxInFlight
        {
            get
            {
                lock (gate)
                {
                    return maxInFlight;
                }
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "At least one request must be allowed in flight.");
                }

                lock (gate)
                {
                    maxInFlight = value;
                }

                GrantWhileFree();
            }
        }

        /// <summary>Requests holding a slot: sent, or ready to be sent.</summary>
        internal int InFlight
        {
            get
            {
                lock (gate)
                {
                    return inFlight;
                }
            }
        }

        /// <summary>Requests waiting for a slot.</summary>
        internal int Queued
        {
            get
            {
                lock (gate)
                {
                    return playerFacing.Count + background.Count;
                }
            }
        }

        /// <summary>
        /// Queues one request. Await <see cref="Ticket.Ready"/> before sending it, then dispose the
        /// ticket when the provider has answered or failed, which frees the slot for the next.
        /// </summary>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> is already cancelled; nothing is queued.</exception>
        internal Ticket Enqueue(DecisionPriority priority, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var ticket = new Ticket(this, priority, ct);
            lock (gate)
            {
                // Everything that goes before it: what holds a slot, the player-facing queue, and
                // for a background request the background queue too.
                ticket.QueuedAhead = inFlight + playerFacing.Count
                    + (priority == DecisionPriority.Background ? background.Count : 0);
                ticket.Node = QueueFor(priority).AddLast(ticket);
            }

            // Registered outside the lock, since a token cancelled meanwhile runs the callback
            // right here, and the callback takes the lock.
            if (ct.CanBeCanceled)
            {
                ticket.Registration = ct.Register(ticket.Withdraw);
            }

            GrantWhileFree();
            return ticket;
        }

        // Hands free slots to the head of the queue until either runs out.
        private void GrantWhileFree()
        {
            lock (gate)
            {
                if (granting)
                {
                    return;
                }

                granting = true;
            }

            try
            {
                while (true)
                {
                    Ticket next;
                    lock (gate)
                    {
                        if (inFlight >= maxInFlight || !TryDequeue(out next))
                        {
                            granting = false;
                            return;
                        }

                        inFlight++;
                        next.MarkReady();
                    }

                    // Outside the lock: the waiting decision may resume right here and send.
                    next.Source.TrySetResult();
                }
            }
            catch
            {
                lock (gate)
                {
                    granting = false;
                }

                throw;
            }
        }

        private bool TryDequeue(out Ticket next)
        {
            var queue = playerFacing.Count > 0 ? playerFacing : background;
            if (queue.Count == 0)
            {
                next = null;
                return false;
            }

            next = queue.First.Value;
            queue.RemoveFirst();
            return true;
        }

        private LinkedList<Ticket> QueueFor(DecisionPriority priority)
        {
            return priority == DecisionPriority.Background ? background : playerFacing;
        }

        /// <summary>
        /// One request's place in the queue, then its slot. Disposing it frees the slot, or, while it
        /// is still queued, withdraws it; either happens once however often it is disposed.
        /// </summary>
        internal sealed class Ticket : IDisposable
        {
            private enum Stage
            {
                Queued,
                Ready,
                Finished,
                Withdrawn,
            }

            private readonly RequestScheduler scheduler;
            private readonly CancellationToken token;
            private Stage stage;

            internal Ticket(RequestScheduler scheduler, DecisionPriority priority, CancellationToken token)
            {
                this.scheduler = scheduler;
                this.token = token;
                Priority = priority;
            }

            /// <summary>The priority it was queued with.</summary>
            internal DecisionPriority Priority { get; }

            /// <summary>
            /// How many requests were ahead of it when it was queued: those holding a slot, and those
            /// queued that go before it. A request queued later with a higher priority is not counted.
            /// </summary>
            internal int QueuedAhead { get; set; }

            /// <summary>
            /// Completes when it is this request's turn to be sent, or throws
            /// <see cref="OperationCanceledException"/> if it was cancelled while queued. Await it
            /// once: Unity pools an <c>Awaitable</c> once it has been awaited.
            /// </summary>
            internal Awaitable Ready => Source.Awaitable;

            /// <summary>Still waiting for a slot.</summary>
            internal bool IsQueued
            {
                get
                {
                    lock (scheduler.gate)
                    {
                        return stage == Stage.Queued;
                    }
                }
            }

            /// <summary>Holding a slot: its turn has come, and it has not been disposed.</summary>
            internal bool IsReady
            {
                get
                {
                    lock (scheduler.gate)
                    {
                        return stage == Stage.Ready;
                    }
                }
            }

            // Completed exactly once, by whichever moves it out of Stage.Queued under the lock.
            // Never touched again after that: once awaited, its Awaitable goes back to Unity's pool.
            internal AwaitableCompletionSource Source { get; } = new AwaitableCompletionSource();

            internal LinkedListNode<Ticket> Node { get; set; }

            internal CancellationTokenRegistration Registration { get; set; }

            /// <summary>Frees its slot, or withdraws it if it is still queued.</summary>
            public void Dispose()
            {
                Registration.Dispose();

                // Decided in one critical section, so a slot granted on another thread between
                // looking and acting can never be left held by a ticket nobody will dispose again.
                bool freed;
                lock (scheduler.gate)
                {
                    if (stage == Stage.Ready)
                    {
                        stage = Stage.Finished;
                        scheduler.inFlight--;
                        freed = true;
                    }
                    else if (LeaveQueue())
                    {
                        freed = false;
                    }
                    else
                    {
                        return;
                    }
                }

                if (freed)
                {
                    // A failed request frees its slot the same way: the caller disposes in a finally.
                    scheduler.GrantWhileFree();
                }
                else
                {
                    Source.TrySetException(new OperationCanceledException(token));
                }
            }

            // Called under the scheduler's lock, as the ticket leaves the queue for a slot.
            internal void MarkReady()
            {
                stage = Stage.Ready;
            }

            // The cancellation callback: a queued request leaves the queue at once and its waiter
            // ends cancelled. Too late once its turn has come; then the token goes to the provider.
            internal void Withdraw()
            {
                lock (scheduler.gate)
                {
                    if (!LeaveQueue())
                    {
                        return;
                    }
                }

                // Taken off the queue, not out of a slot, so nothing new can be sent: the requests
                // behind it simply move up.
                Source.TrySetException(new OperationCanceledException(token));
            }

            // Called under the scheduler's lock. True when it was still queued and has now left.
            private bool LeaveQueue()
            {
                if (stage != Stage.Queued)
                {
                    return false;
                }

                stage = Stage.Withdrawn;
                Node.List.Remove(Node);
                return true;
            }
        }
    }
}
