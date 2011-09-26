using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

public static class AsyncNetwork {
    public static Dictionary<TActor, BigInteger> Run<TParticipant, TActor, TMessage>(int syncLimit, IEnumerable<TActor> roundActors, int? maxRound = default(int?))
        where TActor : TParticipant, IActor<TParticipant, TMessage> {
        return AsyncNetwork<TParticipant, TActor, TMessage>.Run(syncLimit, roundActors, maxRound);
    }
}
public interface IActor<TParticipant, TMessage> {
    void Init(IEnumerable<TParticipant> players);
    Dictionary<TParticipant, TMessage> StartRound(int round);
    EndRoundResult EndRound(int round, Dictionary<TParticipant, TMessage> receivedMessages);
}
public interface IActorPlayer<TMessage> : IPlayer, IActor<IPlayer, TMessage> {
}
public static class AsyncNetwork<TParticipant, TActor, TMessage> where TActor : TParticipant, IActor<TParticipant, TMessage> {
    public static Dictionary<TActor, BigInteger> Run(int syncLimit, IEnumerable<TActor> actors, int? maxRound = default(int?)) {
        int round = 0;
        var result = new Dictionary<TActor, BigInteger>();
        foreach (var a in actors)
            a.Init(actors.Cast<TParticipant>());
        var unfinished = new HashSet<TActor>(actors);
        while (unfinished.Any()) {
            if (maxRound != null && round >= maxRound) throw new InvalidOperationException("Failed");

            int sendCount = 0;
            var pendingMessages = actors.ToDictionary(e => (TParticipant)e, e => new Dictionary<TParticipant, TMessage>());
            foreach (var sender in actors) {
                var messages = sender.StartRound(round);
                if (messages != null && messages.Count > 0) {
                    sendCount += 1;
                    foreach (var receiver in messages.Keys)
                        pendingMessages[receiver][sender] = messages[receiver];
                }
            }
            if (sendCount > syncLimit) throw new InvalidOperationException("Exceeded sync limit");

            foreach (var receiver in actors) {
                var r = receiver.EndRound(round, pendingMessages[receiver]);
                if (r.Failed) unfinished.Remove(receiver);
                if (r.OptionalResult.HasValue) {
                    result[receiver] = r.OptionalResult.Value;
                    unfinished.Remove(receiver);
                }
            }

            round += 1;
        }
        return result;
    }
}
