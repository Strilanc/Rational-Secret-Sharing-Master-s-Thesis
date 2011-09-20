using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;

public static class AsyncNetwork<TParticipant, TMessage> {
    public interface IActor {
        void Init(IEnumerable<TParticipant> players);
        Dictionary<TParticipant, TMessage> GetRoundMessages(int round);
        void ReceiveMessage(int round, TParticipant sender, TMessage message);
        EndRoundResult EndRound(int round);
    }

    public static Dictionary<T, BigInteger> Run<T>(IEnumerable<T> roundActors, ISecureRandomNumberGenerator rng) where T : IActor, TParticipant {
        int round = 0;
        var result = new Dictionary<T, BigInteger>();
        foreach (var a in roundActors)
            a.Init(roundActors.Cast<TParticipant>());
        var activeActors = new HashSet<T>(roundActors);
        while (activeActors.Except(result.Keys).Any()) {
            foreach (var sender in activeActors.Shuffle(rng)) {
                var messages = sender.GetRoundMessages(round);
                foreach (var receiver in activeActors.Where(e => messages.ContainsKey(e))) {
                    receiver.ReceiveMessage(round, sender, messages[receiver]);
                }
            }
            
            foreach (var actor in activeActors.ToArray()) {
                var r = actor.EndRound(round);
                if (r.Finished) activeActors.Remove(actor);
                if (r.OptionalResult.HasValue) result[actor] = r.OptionalResult.Value;
            }

            round += 1;
        }
        return result;
    }
}
