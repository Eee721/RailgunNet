﻿/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Collections;
using System.Collections.Generic;

using CommonTools;

namespace Railgun
{
  /// <summary>
  /// States are attached to entities and contain user-defined data. They are
  /// responsible for encoding and decoding that data, and delta-compression.
  /// </summary>
  public abstract class RailEvent : IRailPoolable<RailEvent>
  {
    internal const int UNLIMITED = -1;

    IRailPool<RailEvent> IRailPoolable<RailEvent>.Pool { get; set; }
    void IRailPoolable<RailEvent>.Reset() { this.Reset(); }

    /// <summary>
    /// An id assigned to this event, used for reliability.
    /// </summary>
    internal EventId EventId { get; set; }

    /// <summary>
    /// The tick (sender-side) that this event was generated.
    /// </summary>
    internal Tick Tick { get; set; }

    /// <summary>
    /// The maximum age for this event. Used for entity events.
    /// This value is not synchronized.
    /// </summary>
    internal Tick Expiration { get; set; }

    /// <summary>
    /// The int index for the type of event.
    /// </summary>
    protected internal int EventType { get; set; }

    internal abstract void SetDataFrom(RailEvent other);

    protected abstract void EncodeData(BitBuffer buffer);
    protected abstract void DecodeData(BitBuffer buffer);
    protected abstract void ResetData();

    protected internal virtual void Invoke() { }
    protected internal virtual void Invoke(RailEntity entity) { }

    internal void Initialize(int eventType)
    {
      this.EventType = eventType;
    }

    internal RailEvent Clone()
    {
      RailEvent clone = RailResource.Instance.AllocateEvent(this.EventType);
      clone.EventId = this.EventId;
      clone.Tick = this.Tick;
      clone.Expiration = this.Expiration;
      clone.SetDataFrom(this);
      return clone;
    }

    protected internal void Reset()
    {
      this.EventId = EventId.INVALID;
      this.Tick = Tick.INVALID;
      this.Expiration = Tick.INVALID;
      this.ResetData();
    }

    #region Encode/Decode/etc.
    internal void Encode(BitBuffer buffer)
    {
      // Write: [Tick]
      buffer.Write(RailEncoders.Tick, this.Tick);

      // Write: [Contents]
      this.EncodeContents(buffer);
    }

    internal void Encode(BitBuffer buffer, Tick latestTick)
    {
      TickSpan span = TickSpan.Create(latestTick, this.Tick);
      CommonDebug.Assert(span.IsInRange);

      // Write: [TickSpan]
      buffer.Write(RailEncoders.TickSpan, span);

      // Write: [Contents]
      this.EncodeContents(buffer);
    }

    internal static RailEvent Decode(BitBuffer buffer)
    {
      // Read: [Tick]
      Tick tick = buffer.Read(RailEncoders.Tick);

      // Read: [Contents]
      return RailEvent.DecodeContents(buffer, tick);
    }

    internal static RailEvent Decode(BitBuffer buffer, Tick latestTick)
    {
      // Read: [TickSpan]
      TickSpan span = buffer.Read(RailEncoders.TickSpan);
      CommonDebug.Assert(span.IsInRange);
      Tick tick = Tick.Create(latestTick, span);

      // Read: [Contents]
      return RailEvent.DecodeContents(buffer, tick);
    }

    private void EncodeContents(
      BitBuffer buffer)
    {
      // Write: [EventType]
      buffer.Write(RailEncoders.EventType, this.EventType);

      // Write: [EventId]
      buffer.Write(RailEncoders.EventId, this.EventId);

      // Write: [EventData]
      this.EncodeData(buffer);
    }

    private static RailEvent DecodeContents(
      BitBuffer buffer, Tick tick)
    {
      // Read: [EventType]
      int eventType = buffer.Read(RailEncoders.EventType);

      RailEvent evnt = RailResource.Instance.AllocateEvent(eventType);
      evnt.Tick = tick;

      // Read: [EventId]
      evnt.EventId = buffer.Read(RailEncoders.EventId);

      // Read: [EventData]
      evnt.DecodeData(buffer);

      return evnt;
    }
    #endregion
  }

  /// <summary>
  /// This is the class to override to attach user-defined data to an entity.
  /// </summary>
  public abstract class RailEvent<T> : RailEvent
    where T : RailEvent<T>, new()
  {
    #region Casting Overrides
    internal override void SetDataFrom(RailEvent other)
    {
      this.SetDataFrom((T)other);
    }
    #endregion

    protected internal abstract void SetDataFrom(T other);
  }
}
