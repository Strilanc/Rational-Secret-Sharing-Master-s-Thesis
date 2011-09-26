using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Diagnostics.Contracts;

public static class AsyncNetwork {
    public static Dictionary<TActor, BigInteger> Run<TParticipant, TActor, TMessage>(int syncLimit, IEnumerable<TActor> roundActors)
        where TActor : TParticipant, IActor<TParticipant, TMessage> {
        return AsyncNetwork<TParticipant, TActor, TMessage>.Run(syncLimit, roundActors);
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
    public static Dictionary<TActor, BigInteger> Run(int syncLimit, IEnumerable<TActor> actors) {
        int round = 0;
        var result = new Dictionary<TActor, BigInteger>();
        foreach (var a in actors)
            a.Init(actors.Cast<TParticipant>());
        var active = new HashSet<TActor>(actors);
        while (active.Except(result.Keys).Any()) {
            int sendCount = 0;
            var pendingMessages = active.ToDictionary(e => (TParticipant)e, e => new Dictionary<TParticipant, TMessage>());
            foreach (var sender in active) {
                var messages = sender.StartRound(round);
                if (messages != null && messages.Count > 0) {
                    sendCount += 1;
                    foreach (var receiver in messages.Keys.Intersect(active.Cast<TParticipant>())) 
                        pendingMessages[receiver][sender] = messages[receiver];
                }
            }
            if (sendCount > syncLimit) throw new InvalidOperationException("Exceeded sync limit");

            foreach (var receiver in active.ToArray()) {
                var r = receiver.EndRound(round, pendingMessages[receiver]);
                if (r.Finished) active.Remove(receiver);
                if (r.OptionalResult.HasValue) result[receiver] = r.OptionalResult.Value;
            }

            round += 1;
        }
        return result;
    }
}
