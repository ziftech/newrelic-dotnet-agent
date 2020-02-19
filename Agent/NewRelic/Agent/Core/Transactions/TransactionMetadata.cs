using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Transactions
{
	public interface ITransactionMetadata : ITransactionAttributeMetadata
	{
		ImmutableTransactionMetadata ConvertToImmutableMetadata();
		string CrossApplicationReferrerPathHash { get; }
		string CrossApplicationReferrerProcessId { get; }
		string CrossApplicationReferrerTripId { get; }
		string CrossApplicationReferrerTransactionGuid { get; }
		float CrossApplicationResponseTimeInSeconds { get; }

		string DistributedTraceType { get; set; }
		string DistributedTraceAppId { get; set; }
		string DistributedTraceAccountId { get; set; }
		string DistributedTraceTransportType { get; }
		string DistributedTraceGuid { get; set; }
		TimeSpan DistributedTraceTransportDuration { get; set; }
		string DistributedTraceTraceId { get; set; }
		string DistributedTraceTrustKey { get; set; }
		string DistributedTraceTransactionId { get; set; }
		bool? DistributedTraceSampled { get; set; }
		bool HasOutgoingDistributedTracePayload { get; set; }
		bool HasIncomingDistributedTracePayload { get; set; }
		string SyntheticsResourceId { get; }
		string SyntheticsJobId { get; }
		string SyntheticsMonitorId { get; }
		string LatestCrossApplicationPathHash { get; }
		void SetUri(string uri);
		void SetOriginalUri(string uri);
		void SetReferrerUri(string uri);
		void SetQueueTime(TimeSpan queueTime);
		void AddRequestParameter(string key, string value);
		void AddUserAttribute(string key, object value);
		void AddUserErrorAttribute(string key, object value);
		void SetHttpResponseStatusCode(int statusCode, int? subStatusCode);
		void AddExceptionData(ErrorData errorData);
		void AddCustomErrorData(ErrorData errorData);
		void SetCrossApplicationReferrerTripId(string tripId);
		void SetCrossApplicationReferrerPathHash(string referrerPathHash);
		void SetCrossApplicationReferrerProcessId(string referrerProcessId);
		void SetCrossApplicationReferrerContentLength(long referrerContentLength);
		void SetCrossApplicationReferrerTransactionGuid(string transactionGuid);
		void SetCrossApplicationPathHash(string pathHash);
		void SetCrossApplicationResponseTimeInSeconds(float responseTimeInSeconds);
		void SetDistributedTraceTransportType(TransportType transportType);
		void SetSyntheticsResourceId(string syntheticsResourceId);
		void SetSyntheticsJobId(string syntheticsJobId);
		void SetSyntheticsMonitorId(string syntheticsMonitorId);
		void MarkHasCatResponseHeaders();

		long GetCrossApplicationReferrerContentLength();

		bool IsSynthetics { get; }
		float Priority { get; set; }

		void SetSampled(IAdaptiveSampler adaptiveSampler);
	}

	/// <summary>
	/// An object for a collection of optional transaction metadata.
	/// </summary>
	public class TransactionMetadata : ITransactionMetadata
	{
		private readonly object _sync = new object();
		//This mapping needs to be kept in-sync with the TransportType enum
		public static readonly string[] TransportTypeToStringMapping = new[]
		{
			"Unknown",
			"HTTP",
			"HTTPS",
			"Kafka",
			"JMS",
			"IronMQ",
			"AMQP",
			"Queue",
			"Other"
		};

		// These are all volatile because they can be read before the transaction is completed.
		// These can be written by one thread and read by another.
		private volatile string _crossApplicationReferrerPathHash;
		private volatile string _crossApplicationReferrerProcessId;
		private volatile string _crossApplicationReferrerTripId;
		private volatile string _crossApplicationReferrerTransactionGuid;
		private volatile float _crossApplicationResponseTimeInSeconds = 0;

		private volatile string _distributedTraceType;
		private volatile string _distributedTraceAppId;
		private volatile string _distributedTraceAccountId;
		private volatile string _distributedTraceTransportType;
		private volatile string _distributedTraceGuid;
		private TimeSpan _distributedTraceTransportDuration;
		private volatile string _distributedTraceTraceId;
		private volatile string _distributedTraceTrustKey;
		private volatile string _distributedTraceTransactionId;

		private volatile string _syntheticsResourceId;
		private volatile string _syntheticsJobId;
		private volatile string _syntheticsMonitorId;
		private volatile string _latestCrossApplicationPathHash;

		//if this never gets set, then default to -1
		// thread safety for this occurrs in the getter and setter below
		private long _crossApplicationReferrerContentLength = -1;
		//This is a timeSpan? struct
		private volatile Func<TimeSpan> _timeSpanQueueTime = null;
		//This is a Int32? struct
		private volatile int _httpResponseStatusCode = int.MinValue;

		private volatile string _uri;
		private volatile string _originalUri;
		private volatile string _referrerUri;

		private readonly ConcurrentDictionary<string, string> _requestParameters = new ConcurrentDictionary<string, string>();
		private readonly ConcurrentDictionary<string, object> _userAttributes = new ConcurrentDictionary<string, object>();
		private readonly ConcurrentDictionary<string, object> _userErrorAttributes = new ConcurrentDictionary<string, object>();

		//everything below this does not have a getter, meaning it is only updated and not read during the transaction

		private readonly IList<ErrorData> _transactionExceptionDatas = new ConcurrentList<ErrorData>();
		private readonly IList<ErrorData> _customErrorDatas = new ConcurrentList<ErrorData>();
		private readonly ConcurrentHashSet<string> _allCrossApplicationPathHashes = new ConcurrentHashSet<string>();

		private volatile int _httpResponseSubStatusCode = int.MinValue;
		private volatile bool _hasResponseCatHeaders;
		private volatile float _priority;

		public ImmutableTransactionMetadata ConvertToImmutableMetadata()
		{
			var alternateCrossApplicationPathHashes = _allCrossApplicationPathHashes
				.Except(new[] { _latestCrossApplicationPathHash })
				.Take(PathHashMaker.AlternatePathHashMaxSize);

			var sampled = DistributedTraceSampled.HasValue ? DistributedTraceSampled.Value : false;
			return new ImmutableTransactionMetadata(_uri, _originalUri, _referrerUri, GetTimeSpan(), _requestParameters, _userAttributes, _userErrorAttributes, HttpResponseStatusCode, HttpResponseSubStatusCode, _transactionExceptionDatas, _customErrorDatas, _crossApplicationReferrerPathHash, _latestCrossApplicationPathHash, alternateCrossApplicationPathHashes, _crossApplicationReferrerTransactionGuid, _crossApplicationReferrerProcessId, _crossApplicationReferrerTripId, _crossApplicationResponseTimeInSeconds, DistributedTraceType, DistributedTraceAppId, DistributedTraceAccountId, DistributedTraceTransportType, DistributedTraceGuid, DistributedTraceTransportDuration, DistributedTraceTraceId, DistributedTraceTrustKey, DistributedTraceTransactionId, sampled, HasOutgoingDistributedTracePayload, HasIncomingDistributedTracePayload, _syntheticsResourceId, _syntheticsJobId, _syntheticsMonitorId, IsSynthetics, _hasResponseCatHeaders, Priority);
		}

		public float Priority
		{
			get => _priority;
			set => _priority = value;
		}

		public void SetSampled(IAdaptiveSampler adaptiveSampler)
		{
			lock (_sync)
			{
				if (!DistributedTraceSampled.HasValue)
				{
					var priority = _priority;
					DistributedTraceSampled = adaptiveSampler.ComputeSampled(ref priority);
					_priority = priority;
				}
			}
		}

		public bool IsSynthetics => !string.IsNullOrEmpty(_syntheticsResourceId) && !string.IsNullOrEmpty(_syntheticsJobId) &&
									 !string.IsNullOrEmpty(_syntheticsMonitorId);

		public bool IsDistributedTraceParticipant => _distributedTraceGuid != null;

		public void SetUri(string uri)
		{
			_uri = uri;
		}

		public void SetOriginalUri(string uri)
		{
			_originalUri = uri;
		}

		public void SetReferrerUri(string uri)
		{
			_referrerUri = uri;
		}

		public void SetQueueTime(TimeSpan queueTime)
		{
			_timeSpanQueueTime = () => queueTime;
		}

		public void AddRequestParameter(string key, string value)
		{
			_requestParameters[key] = value;
		}

		public void AddUserAttribute(string key, object value)
		{
			// A context switch is possible between calls to Count and AddOrUpdate.
			// This makes the following logic somewhat bogus. That is, it is possible for more attributes to be added than allowed by UserAttributeClamp.
			// However, the AttributeService still enforces UserAttributeClamp on the back end.

			if (_userAttributes.Count >= AttributeCollection.UserAttributeClamp)
			{
				Log.Debug($"User Attribute discarded: {key}. User limit of 64 reached.");
				return;
			}

			_userAttributes[key] = value;
		}

		public void AddUserErrorAttribute(string key, object value)
		{
			_userErrorAttributes[key] = value;
		}

		public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode)
		{
			_httpResponseStatusCode = statusCode;
			_httpResponseSubStatusCode = subStatusCode.HasValue ? (int)subStatusCode : int.MinValue;
		}

		public void AddExceptionData(ErrorData errorData)
		{
			_transactionExceptionDatas.Add(errorData);
		}

		public void AddCustomErrorData(ErrorData errorData)
		{
			_customErrorDatas.Add(errorData);
		}

		public void SetCrossApplicationReferrerPathHash(string referrerPathHash)
		{
			_crossApplicationReferrerPathHash = referrerPathHash;
		}

		public void SetCrossApplicationReferrerProcessId(string referrerProcessId)
		{
			_crossApplicationReferrerProcessId = referrerProcessId;
		}

		public void SetCrossApplicationReferrerContentLength(long contentLength)
		{
			Interlocked.Exchange(ref _crossApplicationReferrerContentLength, contentLength);
		}

		public void SetCrossApplicationReferrerTransactionGuid(string transactionGuid)
		{
			_crossApplicationReferrerTransactionGuid = transactionGuid;
		}

		public void SetCrossApplicationPathHash(string pathHash)
		{
			_latestCrossApplicationPathHash = pathHash;
			_allCrossApplicationPathHashes.Add(pathHash);
		}

		public void SetCrossApplicationReferrerTripId(string referrerTripId)
		{
			_crossApplicationReferrerTripId = referrerTripId;
		}

		public void SetCrossApplicationResponseTimeInSeconds(float responseTimeInSeconds)
		{
			Interlocked.Exchange(ref _crossApplicationResponseTimeInSeconds, responseTimeInSeconds);
		}

		public void SetSyntheticsResourceId(string syntheticsResourceId)
		{
			_syntheticsResourceId = syntheticsResourceId;
		}
		public void SetSyntheticsJobId(string syntheticsJobId)
		{
			_syntheticsJobId = syntheticsJobId;
		}
		public void SetSyntheticsMonitorId(string syntheticsMonitorId)
		{
			_syntheticsMonitorId = syntheticsMonitorId;
		}

		public void MarkHasCatResponseHeaders()
		{
			_hasResponseCatHeaders = true;
		}

		public long GetCrossApplicationReferrerContentLength()
		{
			return Interlocked.Read(ref _crossApplicationReferrerContentLength);
		}

		public string SyntheticsJobId => _syntheticsJobId;
		public string SyntheticsMonitorId => _syntheticsMonitorId;
		public string SyntheticsResourceId => _syntheticsResourceId;
		public string CrossApplicationReferrerPathHash => _crossApplicationReferrerPathHash;
		public string CrossApplicationReferrerTripId => _crossApplicationReferrerTripId;
		public string CrossApplicationReferrerProcessId => _crossApplicationReferrerProcessId;
		public string CrossApplicationReferrerTransactionGuid => _crossApplicationReferrerTransactionGuid;
		public string LatestCrossApplicationPathHash => _latestCrossApplicationPathHash;
		public float CrossApplicationResponseTimeInSeconds => _crossApplicationResponseTimeInSeconds;

		public string DistributedTraceType
		{
			get => _distributedTraceType;
			set => _distributedTraceType = value;
		}
		public string DistributedTraceAppId
		{
			get => _distributedTraceAppId;
			set => _distributedTraceAppId = value;
		}
		public string DistributedTraceAccountId
		{
			get => _distributedTraceAccountId;
			set => _distributedTraceAccountId = value;
		}

		private static string SanitizeTransportType(TransportType transportType)
		{
			var transportTypeValue = (int)transportType;
			if (transportTypeValue >= 0 && transportTypeValue < TransportTypeToStringMapping.Length)
			{
				return TransportTypeToStringMapping[transportTypeValue];
			}

			return TransportTypeToStringMapping[0]; //Use "Unknown" if there was no valid mapping defined
		}

		public string DistributedTraceTransportType
		{
			get => _distributedTraceTransportType;
		}

		public void SetDistributedTraceTransportType(TransportType transportType)
		{
			_distributedTraceTransportType = SanitizeTransportType(transportType);
		}

		public string DistributedTraceGuid
		{
			get => _distributedTraceGuid;
			set => _distributedTraceGuid = value;
		}

		public string DistributedTraceTransactionId
		{
			get => _distributedTraceTransactionId;
			set => _distributedTraceTransactionId = value;
		}

		public TimeSpan DistributedTraceTransportDuration
		{
			get => _distributedTraceTransportDuration;
			set => _distributedTraceTransportDuration = value;
		}
		public string DistributedTraceTraceId
		{
			get => _distributedTraceTraceId;
			set => _distributedTraceTraceId = value;
		}

		public string DistributedTraceTrustKey
		{
			get => _distributedTraceTrustKey;
			set => _distributedTraceTrustKey = value;
		}

		public bool? DistributedTraceSampled { get; set; }

		public bool HasOutgoingDistributedTracePayload { get; set; }

		public bool HasIncomingDistributedTracePayload { get; set; }

		public string Uri => _uri;
		public string OriginalUri => _originalUri;
		public string ReferrerUri => _referrerUri;

		public TimeSpan? QueueTime => GetTimeSpan();

		private TimeSpan? GetTimeSpan() => _timeSpanQueueTime?.Invoke();

		public int? HttpResponseStatusCode => _httpResponseStatusCode == int.MinValue ? default(int?) : _httpResponseStatusCode;

		private int? HttpResponseSubStatusCode => _httpResponseSubStatusCode == int.MinValue ? default(int?) : _httpResponseSubStatusCode;

		public KeyValuePair<string, string>[] RequestParameters => _requestParameters.ToArray();
		public KeyValuePair<string, object>[] UserAttributes => _userAttributes.ToArray();
		public KeyValuePair<string, object>[] UserErrorAttributes => _userErrorAttributes.ToArray();
	}
}