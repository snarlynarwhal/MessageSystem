﻿using HHG.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HHG.Messages
{
    public partial class Message
    {
        internal class Default : IMessage
        {
            private static readonly Dictionary<SubjectId, List<Subscription>> actionSubscriptions = new Dictionary<SubjectId, List<Subscription>>();
            private static readonly Dictionary<SubjectId, List<Subscription>> funcSubscriptions = new Dictionary<SubjectId, List<Subscription>>();

            #region Publish

            public void Publish(object message)
            {
                Publish(null, message);
            }

            public void Publish(object id, object message, PublishMode mode = PublishMode.Broadcast)
            {
                SubjectId subjectId = new SubjectId(message.GetType(), id);

                if (!actionSubscriptions.ContainsKey(subjectId))
                {
                    return;
                }

                for (int i = 0; i < actionSubscriptions[subjectId].Count; i++)
                {
                    Subscription subscription = actionSubscriptions[subjectId][i];
                    subscription.InvokeAction(message);

                    if (message is ICancellable cancellable && cancellable.IsCancelled)
                    {
                        return;
                    }
                }

                if (id != null && mode == PublishMode.Broadcast)
                {
                    Publish(null, message);
                }
            }

            public void PublishTo(object id, object message)
            {
                Publish(id, message, PublishMode.Narrowcast);
            }

            #endregion

            #region Publish

            public R[] Publish<R>(object message)
            {
                return Publish<R>(null, message);
            }

            public R[] Publish<R>(object id, object message, PublishMode mode = PublishMode.Broadcast)
            {
                Type type = message.GetType();

                SubjectId subjectId = new SubjectId(type, id);

                if (!funcSubscriptions.ContainsKey(subjectId))
                {
                    return new R[0];
                }

                int global = 0;
                int size = funcSubscriptions[subjectId].Count;
                if (id != null && mode == PublishMode.Broadcast)
                {
                    SubjectId nullSubjectId = new SubjectId(type, null);
                    global = funcSubscriptions[nullSubjectId].Count;
                    size += global;
                }

                R[] retval = new R[size];

                int i = 0;
                for (int i1 = 0; i1 < funcSubscriptions[subjectId].Count; i1++)
                {
                    Subscription subscription = funcSubscriptions[subjectId][i1];
                    retval[i++] = (R)subscription.InvokeFunc(message);

                    if (message is ICancellable cancellable && cancellable.IsCancelled)
                    {
                        Array.Resize(ref retval, i);
                        return retval;
                    }
                }

                if (id != null && global > 0)
                {
                    Array.Copy(Publish<R>(null, message), 0, retval, i, global);
                }

                return retval;
            }

            public R[] PublishTo<R>(object id, object message)
            {
                return Publish<R>(id, message, PublishMode.Narrowcast);
            }

            #endregion

            #region Request

            public R Publish<R>(IRequest<R> request)
            {
                Publish((object)request);
                return request.Response;
            }

            public R Publish<R>(object id, IRequest<R> request, PublishMode mode = PublishMode.Broadcast)
            {
                Publish(id, (object)request, mode);
                return request.Response;
            }

            public R PublishTo<R>(object id, IRequest<R> request)
            {
                PublishTo(id, (object)request);
                return request.Response;
            }

            #endregion

            #region Aggregate

            public R Publish<R>(IAggregate<R> aggregate)
            {
                R[] results = Publish<R>((object)aggregate);
                return results.Aggregate(aggregate.Aggregate);
            }

            public R Publish<R>(object id, IAggregate<R> aggregate, PublishMode mode = PublishMode.Broadcast)
            {
                R[] results = Publish<R>(id, (object)aggregate, mode);
                return results.Aggregate(aggregate.Aggregate);
            }

            public R PublishTo<R>(object id, IAggregate<R> aggregate)
            {
                R[] results = PublishTo<R>(id, (object)aggregate);
                return results.Aggregate(aggregate.Aggregate);
            }

            #endregion

            #region Subscribe (Publishes)


            public void Subscribe<T>(Action<T> callback, int order = 0)
            {
                SubscribeInternal<T>(null, callback, order);
            }

            public void Subscribe<T>(object id, Action<T> callback, int order = 0)
            {
                SubscribeInternal<T>(id, callback, order);
            }

            protected void SubscribeInternal<T>(object id, Delegate callback, int order = 0)
            {
                Action<object> wrappedCallback = default;
                if (callback is Action action)
                {
                    wrappedCallback = arg => action();
                }
                else if (callback is Action<T> actionWithParam)
                {
                    wrappedCallback = arg => actionWithParam((T)arg);
                }
                SubjectId subjectId = new SubjectId(typeof(T), id);
                SubscriptionId subscriptionId = new SubscriptionId(subjectId, callback);
                Subscription subscription = new Subscription(subscriptionId, wrappedCallback, order);

                if (!actionSubscriptions.ContainsKey(subjectId))
                {
                    actionSubscriptions.Add(subjectId, new List<Subscription>());
                }

                if (!actionSubscriptions[subjectId].Contains(subscription))
                {
                    actionSubscriptions[subjectId].Add(subscription);
                    actionSubscriptions[subjectId].Sort();
                }
            }

            public void Unsubscribe<T>(Action<T> callback)
            {
                UnsubscribeInternal<T>(null, callback);
            }

            public void Unsubscribe<T>(object id, Action<T> callback)
            {
                UnsubscribeInternal<T>(id, callback);
            }

            public void UnsubscribeInternal<T>(object id, Delegate callback)
            {
                SubjectId subjectId = new SubjectId(typeof(T), id);
                SubscriptionId subscriptionId = new SubscriptionId(subjectId, callback);

                if (!actionSubscriptions.ContainsKey(subjectId))
                {
                    return;
                }

                for (int i = 0; i < actionSubscriptions[subjectId].Count; i++)
                {
                    if (actionSubscriptions[subjectId][i].SubscriptionId == subscriptionId)
                    {
                        actionSubscriptions[subjectId].RemoveAt(i);
                        break;
                    }
                }
            }

            #endregion

            #region Subscribe (Publishs)

            public void Subscribe<T, R>(Func<T, R> callback, int order = 0)
            {
                Subscribe(null, callback, order);
            }

            public void Subscribe<T, R>(object id, Func<T, R> callback, int order = 0)
            {
                Func<object, object> wrappedCallback = args => callback((T)args);
                SubjectId subjectId = new SubjectId(typeof(T), id);
                SubscriptionId subscriptionId = new SubscriptionId(subjectId, callback);
                Subscription subscription = new Subscription(subscriptionId, wrappedCallback, order);

                if (!funcSubscriptions.ContainsKey(subjectId))
                {
                    funcSubscriptions.Add(subjectId, new List<Subscription>());
                }

                if (!funcSubscriptions[subjectId].Contains(subscription))
                {
                    funcSubscriptions[subjectId].Add(subscription);
                    funcSubscriptions[subjectId].Sort();
                }
            }

            public void Unsubscribe<T, R>(Func<T, R> callback)
            {
                Unsubscribe(null, callback);
            }

            public void Unsubscribe<T, R>(object id, Func<T, R> callback)
            {
                Func<object, object> wrappedCallback = args => callback((T)args);
                SubjectId subjectId = new SubjectId(typeof(T), id);
                SubscriptionId subscriptionId = new SubscriptionId(subjectId, callback);

                if (!funcSubscriptions.ContainsKey(subjectId))
                {
                    return;
                }

                for (int i = 0; i < funcSubscriptions[subjectId].Count; i++)
                {
                    if (funcSubscriptions[subjectId][i].SubscriptionId == subscriptionId)
                    {
                        funcSubscriptions[subjectId].RemoveAt(i);
                        break;
                    }
                }
            }

            #endregion
        }
    }
}