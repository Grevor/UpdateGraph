using System;
using System.Collections.Generic;
using System.Linq;

namespace Updater
{
    public delegate void Updater();

    public class UpdateAction<TUpdateKey>
    {
        public UpdateAction(Updater updater, params TUpdateKey[] triggeredUpdates)
        {
            Updater = updater;
            TriggeredUpdates = triggeredUpdates.ToArray();
        }

        public Updater Updater { get; }
        public IEnumerable<TUpdateKey> TriggeredUpdates { get; }
    }
}
