using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;

public interface ISyncSocket<TParticipant, TMessage> {
    void SetMessageToSendTo(IEnumerable<TParticipant> receivers, TMessage message);
    ISet<TParticipant> GetParticipants();
    Dictionary<TParticipant, TMessage> GetReceivedMessages();
}
public struct EndRoundResult {
    public readonly bool Finished;
    public readonly BigInteger? OptionalResult;
    public EndRoundResult(bool finished = false, BigInteger? optionalResult = default(BigInteger?)) {
        this.Finished = finished;
        this.OptionalResult = optionalResult;
    }
}
public interface IRoundActor {
    void BeginRound(int round);
    EndRoundResult EndRound(int round);
}
public interface IPlayer {
    int Index { get; }
}

public class SyncNetwork<TParticipant, TMessage> {
    private class Socket : ISyncSocket<TParticipant, TMessage> {
        public readonly SyncNetwork<TParticipant, TMessage> Network;
        public readonly TParticipant Participant;
        public Dictionary<TParticipant, TMessage> PendingMessages;

        public Socket(SyncNetwork<TParticipant, TMessage> network, TParticipant participant) {
            this.Network = network;
            this.Participant = participant;
        }

        public void SetMessageToSendTo(IEnumerable<TParticipant> receivers, TMessage message) {
            if (!Network.inRound) throw new InvalidOperationException("Not in a started round.");
            foreach (var r in receivers) {
                var p = Network.sockets[r].PendingMessages;
                if (message == null)
                    p.Remove(Participant);
                else
                    p[Participant] = message;
            }
        }
        public ISet<TParticipant> GetParticipants() {
            return new HashSet<TParticipant>(this.Network.sockets.Keys);
        }
        public Dictionary<TParticipant, TMessage> GetReceivedMessages() {
            if (Network.inRound) throw new InvalidOperationException("In a started round.");
            return PendingMessages;
        }
    }

    private readonly Dictionary<TParticipant, Socket> sockets = new Dictionary<TParticipant,Socket>();
    private bool inRound = false;

    public ISyncSocket<TParticipant, TMessage> Connect(TParticipant participant) {
        if (inRound) throw new InvalidOperationException("Can't connect during a round.");
        if (sockets.ContainsKey(participant)) throw new InvalidOperationException("Already connected");
        var socket = new Socket(this, participant);
        sockets[participant] = socket;
        return socket;
    }

    public void StartRound() {
        if (inRound) throw new InvalidOperationException("Round already started.");
        inRound = true;
        foreach (var socket in sockets.Values) {
            socket.PendingMessages = new Dictionary<TParticipant, TMessage>();
        }
    }
    public void EndRound() {
        if (!inRound) throw new InvalidOperationException("Round already ended.");
        inRound = false;
    }

    public Dictionary<T, BigInteger> Run<T>(IEnumerable<T> roundActors) where T : IRoundActor {
        int round = 0;
        var result = new Dictionary<T, BigInteger>();
        var activeActors = new HashSet<T>(roundActors);
        while (activeActors.Except(result.Keys).Any()) {
            StartRound();
            foreach (var t in activeActors) {
                t.BeginRound(round);
            }

            EndRound();
            foreach (var t in activeActors.ToArray()) {
                var r = t.EndRound(round);
                if (r.Finished) activeActors.Remove(t);
                if (r.OptionalResult.HasValue) result[t] = r.OptionalResult.Value;
            }

            round += 1;
        }
        return result;
    }
}
