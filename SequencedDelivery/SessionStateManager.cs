using System;
using System.Collections.Generic;
using System.Text;

namespace SequencedDelivery
{
  public class SessionStateManager
  {
    public int LastProcessed { get; set; } = 0;
    public Dictionary<int, long> DeferredList { get; set; } = new Dictionary<int, long>();
  }
}
