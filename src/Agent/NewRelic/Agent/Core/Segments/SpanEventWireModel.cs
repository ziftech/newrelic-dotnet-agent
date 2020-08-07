/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.Linq;
using System;
using NewRelic.Agent.Core.Attributes;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Collections;
using NewRelic.Agent.Core.DataTransport;

namespace NewRelic.Agent.Core.Segments
{
    public partial class Span : IStreamingModel, IDisposable
    {
        private static ObjectPool<Span> _objectPool = new ObjectPool<Span>(100, () => new Span());

        public static Span Create()
        {
            return _objectPool.Take();
        }

        public string SpanId { get; set; }

        public string DisplayName => $"{TraceId}.{SpanId}";

        public void Dispose()
        {
            ClearAttributeValues(AgentAttributes);
            ClearAttributeValues(UserAttributes);
            ClearAttributeValues(Intrinsics);

            SpanId = default;
            TraceId = string.Empty;

            _objectPool.Return(this);
        }

        private void ClearAttributeValues(MapField<string,AttributeValue> attribValues)
        {
            foreach(var attribVal in attribValues.Values)
            {
                attribVal.RemoveReference();
            }

            attribValues.Clear();
        }
    }

    public partial class SpanBatch : IStreamingBatchModel<Span>
    {
        public int Count => (Spans?.Count).GetValueOrDefault(0);

        public void OnSuccessfulSend()
        {
            foreach(var span in Spans)
            {
                span.Dispose();
            }

            Spans.Clear();
        }
    }

    [JsonConverter(typeof(SpanEventWireModelSerializer))]
    public interface ISpanEventWireModel : IAttributeValueCollection, IHasPriority, IDisposable
    {
        Span Span { get; }
    }

    public class SpanAttributeValueCollection : AttributeValueCollectionBase<AttributeValue>, ISpanEventWireModel
    {
        public float Priority { get; set; }

        //Since the Map Field may not be concurrrent, we need to lock the objects when performing operations around them
        private readonly Dictionary<AttributeClassification, object> _lockObjects = new Dictionary<AttributeClassification, object>
        {
            { AttributeClassification.AgentAttributes, new object() },
            { AttributeClassification.Intrinsics, new object() },
            { AttributeClassification.UserAttributes, new object() }
        };

        private Span _span;
        public Span Span => _span;

        public void Dispose()
        {
            _span?.Dispose();
        }

        public SpanAttributeValueCollection() : base(AttributeDestinations.SpanEvent)
        {
            _span = Span.Create();
        }

        protected override IEnumerable<AttributeValue> GetAttribValuesImpl(AttributeClassification classification)
        {
            return GetAttribValuesInternal(classification).Values;
        }

        protected override bool SetValueImpl(IAttributeValue value)
        {
            var attribVal = value is AttributeValue
                        ? (AttributeValue)value
                        : AttributeValue.Create(value);

            return SetValueInternal(attribVal);
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, object value)
        {
            var attribVal = AttributeValue.Create(attribDef);
            attribVal.Value = value;

            return SetValueInternal(attribVal);
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, Lazy<object> lazyValue)
        {
            var attribVal = AttributeValue.Create(attribDef);
            attribVal.LazyValue = lazyValue;

            return SetValueInternal(attribVal);
        }

        protected override void RemoveItemsImpl(IEnumerable<AttributeValue> itemsToRemove)
        {
            foreach (var lockObjKVP in _lockObjects)
            {
                var itemsToRemoveForClassification = itemsToRemove
                    .Where(x => x.AttributeDefinition.Classification == lockObjKVP.Key)
                    .ToArray();

                if (itemsToRemoveForClassification.Length == 0)
                {
                    continue;
                }

                var dicForClassification = GetAttribValuesInternal(lockObjKVP.Key);

                lock (lockObjKVP.Value)
                {
                    foreach (var itemToRemove in itemsToRemoveForClassification)
                    {
                        dicForClassification.Remove(itemToRemove.AttributeDefinition.Name);
                        itemToRemove.RemoveReference();
                    }
                }
            }

        }

        private bool SetValueInternal(AttributeValue attribVal)
        {
            //These values are used to create a DisplayName on the Streamable Object
            switch (attribVal.AttributeDefinition.Name)
            {
                case AttributeDefinition.KeyName_TraceId:
                    Span.TraceId = attribVal.StringValue;
                    break;

                case AttributeDefinition.KeyName_Guid:
                    Span.SpanId = attribVal.StringValue;
                    break;
            }

            var dic = GetAttribValuesInternal(attribVal.AttributeDefinition.Classification);

            if (dic == null)
            {
                return false;
            }

            var lockObj = _lockObjects[attribVal.AttributeDefinition.Classification];

            lock (lockObj)
            {
                var existingAttribValue = default(AttributeValue);
                var hasItem = dic.TryGetValue(attribVal.AttributeDefinition.Name, out existingAttribValue);

                //If trying to set the same object ref
                if (hasItem)
                {
                    if (existingAttribValue == attribVal)
                    {
                        return false;
                    }
                    else
                    {
                        existingAttribValue.RemoveReference();
                    }
                }

                dic[attribVal.AttributeDefinition.Name] = attribVal;
                attribVal.AddReference();

                return !hasItem;
            }
        }

        private MapField<string, AttributeValue> GetAttribValuesInternal(AttributeClassification classification)
        {
            switch (classification)
            {
                case AttributeClassification.AgentAttributes:
                    return _span.AgentAttributes;
                case AttributeClassification.UserAttributes:
                    return _span.UserAttributes;
                case AttributeClassification.Intrinsics:
                    return _span.Intrinsics;
                default:
                    return null;
            }
        }
    }
}
