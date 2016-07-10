/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TouchScript.Hit;
using TouchScript.Layers;
using TouchScript.Utils;
using TouchScript.Utils.Attributes;
using TouchScript.Pointers;
using UnityEngine;

namespace TouchScript.Gestures
{
    /// <summary>
    /// Base class for all gestures.
    /// </summary>
    public abstract class Gesture : DebuggableMonoBehaviour
    {
        #region Constants

        /// <summary>
        /// Message sent when gesture changes state if SendMessage is used.
        /// </summary>
        public const string STATE_CHANGE_MESSAGE = "OnGestureStateChange";

        /// <summary>
        /// Message sent when gesture is cancelled if SendMessage is used.
        /// </summary>
        public const string CANCEL_MESSAGE = "OnGestureCancel";

        /// <summary>
        /// Possible states of a gesture.
        /// </summary>
        public enum GestureState
        {
            /// <summary>
            /// Gesture is possible.
            /// </summary>
            Possible,

            /// <summary>
            /// Continuous gesture has just begun.
            /// </summary>
            Began,

            /// <summary>
            /// Started continuous gesture is updated.
            /// </summary>
            Changed,

            /// <summary>
            /// Continuous gesture is ended.
            /// </summary>
            Ended,

            /// <summary>
            /// Gesture is cancelled.
            /// </summary>
            Cancelled,

            /// <summary>
            /// Gesture is failed by itself or by another recognized gesture.
            /// </summary>
            Failed,

            /// <summary>
            /// Gesture is recognized.
            /// </summary>
            Recognized = Ended
        }

        /// <summary>
        /// Current state of the number of pointers.
        /// </summary>
        protected enum PointersNumState
        {
            /// <summary>
            /// The number of pointers is between min and max thresholds.
            /// </summary>
            InRange,

            /// <summary>
            /// The number of pointers is less than min threshold.
            /// </summary>
            TooFew,

            /// <summary>
            /// The number of pointers is greater than max threshold.
            /// </summary>
            TooMany,

            /// <summary>
            /// The number of pointers passed min threshold this frame and is now in range.
            /// </summary>
            PassedMinThreshold,

            /// <summary>
            /// The number of pointers passed max threshold this frame and is now in range.
            /// </summary>
            PassedMaxThreshold,

            /// <summary>
            /// The number of pointers passed both min and max thresholds.
            /// </summary>
            PassedMinMaxThreshold
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when gesture changes state.
        /// </summary>
        public event EventHandler<GestureStateChangeEventArgs> StateChanged
        {
            add { stateChangedInvoker += value; }
            remove { stateChangedInvoker -= value; }
        }

        /// <summary>
        /// Occurs when gesture is cancelled.
        /// </summary>
        public event EventHandler<EventArgs> Cancelled
        {
            add { cancelledInvoker += value; }
            remove { cancelledInvoker -= value; }
        }

        // Needed to overcome iOS AOT limitations
        private EventHandler<GestureStateChangeEventArgs> stateChangedInvoker;
        private EventHandler<EventArgs> cancelledInvoker;

        #endregion

        #region Public properties

        /// <summary>
        /// Gets or sets minimum number of pointers this gesture reacts to.
        /// The gesture will not be recognized if it has less than <see cref="MinPointers"/> pointers.
        /// </summary>
        /// <value> Minimum number of pointers. </value>
        public int MinPointers
        {
            get { return minPointers; }
            set
            {
                if (value < 0) return;
                minPointers = value;
            }
        }

        /// <summary>
        /// Gets or sets maximum number of pointers this gesture reacts to.
        /// The gesture will not be recognized if it has more than <see cref="MaxPointers"/> pointers.
        /// </summary>
        /// <value> Maximum number of pointers. </value>
        public int MaxPointers
        {
            get { return maxPointers; }
            set
            {
                if (value < 0) return;
                maxPointers = value;
            }
        }

        /// <summary>
        /// Gets or sets another gesture which must fail before this gesture can be recognized.
        /// </summary>
        /// <value> The gesture which must fail before this gesture can be recognized. </value>
        public Gesture RequireGestureToFail
        {
            get { return requireGestureToFail; }
            set
            {
                if (requireGestureToFail != null)
                    requireGestureToFail.StateChanged -= requiredToFailGestureStateChangedHandler;
                requireGestureToFail = value;
                if (requireGestureToFail != null)
                    requireGestureToFail.StateChanged += requiredToFailGestureStateChangedHandler;
            }
        }

        /// <summary>
        /// Gets or sets the flag if pointers should be treated as a cluster.
        /// </summary>
        /// <value> <c>true</c> if pointers should be treated as a cluster; otherwise, <c>false</c>. </value>
        /// <remarks>
        /// At the end of a gesture when pointers are lifted off due to the fact that computers are faster than humans the very last pointer's position will be gesture's <see cref="ScreenPosition"/> after that. This flag is used to combine several pointers which from the point of a user were lifted off simultaneously and set their centroid as gesture's <see cref="ScreenPosition"/>.
        /// </remarks>
        public bool CombinePointers
        {
            get { return combinePointers; }
            set { combinePointers = value; }
        }

        /// <summary>
        /// Gets or sets time interval before gesture is recognized to combine all lifted pointers into a cluster to use its center as <see cref="ScreenPosition"/>.
        /// </summary>
        /// <value> Time in seconds to treat pointers lifted off during this interval as a single gesture. </value>
        public float CombinePointersInterval
        {
            get { return combinePointersInterval; }
            set { combinePointersInterval = value; }
        }

        /// <summary>
        /// Gets or sets whether gesture should use Unity's SendMessage in addition to C# events.
        /// </summary>
        /// <value> <c>true</c> if gesture uses SendMessage; otherwise, <c>false</c>. </value>
        public bool UseSendMessage
        {
            get { return useSendMessage; }
            set { useSendMessage = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether state change events are broadcasted if <see cref="UseSendMessage"/> is true..
        /// </summary>
        /// <value> <c>true</c> if state change events should be broadcaster; otherwise, <c>false</c>. </value>
        public bool SendStateChangeMessages
        {
            get { return sendStateChangeMessages; }
            set { sendStateChangeMessages = value; }
        }

        /// <summary>
        /// Gets or sets the target of Unity messages sent from this gesture.
        /// </summary>
        /// <value> The target of Unity messages. </value>
        public GameObject SendMessageTarget
        {
            get { return sendMessageTarget; }
            set
            {
                sendMessageTarget = value;
                if (value == null) sendMessageTarget = gameObject;
            }
        }

        /// <summary>
        /// Gets current gesture state.
        /// </summary>
        /// <value> Current state of the gesture. </value>
        public GestureState State
        {
            get { return state; }
            private set
            {
                PreviousState = state;
                state = value;

                switch (value)
                {
                    case GestureState.Possible:
                        onPossible();
                        break;
                    case GestureState.Began:
                        retainPointers();
                        onBegan();
                        break;
                    case GestureState.Changed:
                        onChanged();
                        break;
                    case GestureState.Recognized:
                        // Only retain/release pointers for continuos gestures
                        if (PreviousState == GestureState.Changed || PreviousState == GestureState.Began)
                            releasePointers(true);
                        onRecognized();
                        break;
                    case GestureState.Failed:
                        onFailed();
                        break;
                    case GestureState.Cancelled:
                        if (PreviousState == GestureState.Changed || PreviousState == GestureState.Began)
                            releasePointers(false);
                        onCancelled();
                        break;
                }

                if (stateChangedInvoker != null)
                    stateChangedInvoker.InvokeHandleExceptions(this,
                        new GestureStateChangeEventArgs(state, PreviousState));
                if (useSendMessage && sendStateChangeMessages && SendMessageTarget != null)
                    sendMessageTarget.SendMessage(STATE_CHANGE_MESSAGE, this, SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// Gets previous gesture state.
        /// </summary>
        /// <value> Previous state of the gesture. </value>
        public GestureState PreviousState { get; private set; }

        /// <summary>
        /// Gets current screen position.
        /// </summary>
        /// <value> Gesture's position in screen coordinates. </value>
        public virtual Vector2 ScreenPosition
        {
            get
            {
                if (NumPointers == 0)
                {
                    if (!TouchManager.IsInvalidPosition(cachedScreenPosition)) return cachedScreenPosition;
                    return TouchManager.INVALID_POSITION;
                }
                return ClusterUtils.Get2DCenterPosition(activePointers);
            }
        }

        /// <summary>
        /// Gets previous screen position.
        /// </summary>
        /// <value> Gesture's previous position in screen coordinates. </value>
        public virtual Vector2 PreviousScreenPosition
        {
            get
            {
                if (NumPointers == 0)
                {
                    if (!TouchManager.IsInvalidPosition(cachedPreviousScreenPosition))
                        return cachedPreviousScreenPosition;
                    return TouchManager.INVALID_POSITION;
                }
                return ClusterUtils.GetPrevious2DCenterPosition(activePointers);
            }
        }

        /// <summary>
        /// Gets normalized screen position.
        /// </summary>
        /// <value> Gesture's position in normalized screen coordinates. </value>
        public Vector2 NormalizedScreenPosition
        {
            get
            {
                var position = ScreenPosition;
                if (TouchManager.IsInvalidPosition(position)) return TouchManager.INVALID_POSITION;
                return new Vector2(position.x / Screen.width, position.y / Screen.height);
            }
        }

        /// <summary>
        /// Gets previous screen position.
        /// </summary>
        /// <value> Gesture's previous position in normalized screen coordinates. </value>
        public Vector2 PreviousNormalizedScreenPosition
        {
            get
            {
                var position = PreviousScreenPosition;
                if (TouchManager.IsInvalidPosition(position)) return TouchManager.INVALID_POSITION;
                return new Vector2(position.x / Screen.width, position.y / Screen.height);
            }
        }

        /// <summary>
        /// Gets list of gesture's active pointers.
        /// </summary>
        /// <value> The list of pointers owned by this gesture. </value>
        public IList<Pointer> ActivePointers
        {
            get
            {
                if (readonlyActivePointers == null)
                    readonlyActivePointers = new ReadOnlyCollection<Pointer>(activePointers);
                return readonlyActivePointers;
            }
        }

        /// <summary>
        /// Gets the number of active pointerss.
        /// </summary>
        /// <value> The number of pointers owned by this gesture. </value>
        public int NumPointers
        {
            get { return numPointers; }
        }

        /// <summary>
        /// Gets or sets an object implementing <see cref="IGestureDelegate"/> to be asked for gesture specific actions.
        /// </summary>
        /// <value> The delegate. </value>
        public IGestureDelegate Delegate { get; set; }

        #endregion

        #region Private variables

        /// <summary>
        /// Reference to global GestureManager.
        /// </summary>
        protected IGestureManager gestureManager
        {
            // implemented as a property because it returns IGestureManager but we need to reference GestureManagerInstance to access internal methods
            get { return gestureManagerInstance; }
        }

        /// <summary>
        /// Reference to global TouchManager.
        /// </summary>
        protected ITouchManager touchManager { get; private set; }

        /// <summary>
        /// The state of min/max number of pointers.
        /// </summary>
        protected PointersNumState pointersNumState { get; private set; }

        /// <summary>
        /// Pointers the gesture currently owns and works with.
        /// </summary>
        protected List<Pointer> activePointers = new List<Pointer>(10);

        /// <summary>
        /// Cached transform of the parent object.
        /// </summary>
        protected Transform cachedTransform;

        [SerializeField]
        private bool advancedProps; // is used to save if advanced properties are opened or closed

        [SerializeField]
        private int minPointers = 0;

        [SerializeField]
        private int maxPointers = 0;

        [SerializeField]
        [ToggleLeft]
        private bool combinePointers = false;

        [SerializeField]
        private float combinePointersInterval = .3f;

        [SerializeField]
        [ToggleLeft]
        private bool useSendMessage = false;

        [SerializeField]
        [ToggleLeft]
        private bool sendStateChangeMessages = false;

        [SerializeField]
        private GameObject sendMessageTarget;

        [SerializeField]
        [NullToggle]
        private Gesture requireGestureToFail;

        [SerializeField]
        // Serialized list of gestures for Unity IDE.
        private List<Gesture> friendlyGestures = new List<Gesture>();

        private int numPointers;
        private TouchLayer layer;
        private ReadOnlyCollection<Pointer> readonlyActivePointers;
        private TimedSequence<Pointer> pointerSequence = new TimedSequence<Pointer>();
        private GestureManagerInstance gestureManagerInstance;
        private GestureState delayedStateChange = GestureState.Possible;
        private bool requiredGestureFailed = false;
        private GestureState state = GestureState.Possible;

        /// <summary>
        /// Cached screen position. 
        /// Used to keep tap's position which can't be calculated from pointers when the gesture is recognized since all pointers are gone.
        /// </summary>
        private Vector2 cachedScreenPosition;

        /// <summary>
        /// Cached previous screen position.
        /// Used to keep tap's position which can't be calculated from pointers when the gesture is recognized since all pointers are gone.
        /// </summary>
        private Vector2 cachedPreviousScreenPosition;

        #endregion

        #region Public methods

        /// <summary>
        /// Adds a friendly gesture.
        /// </summary>
        /// <param name="gesture"> The gesture. </param>
        public void AddFriendlyGesture(Gesture gesture)
        {
            if (gesture == null || gesture == this) return;

            registerFriendlyGesture(gesture);
            gesture.registerFriendlyGesture(this);
        }

        /// <summary>
        /// Checks if a gesture is friendly with this gesture.
        /// </summary>
        /// <param name="gesture"> A gesture to check. </param>
        /// <returns> <c>true</c> if gestures are friendly; <c>false</c> otherwise. </returns>
        public bool IsFriendly(Gesture gesture)
        {
            return friendlyGestures.Contains(gesture);
        }

        /// <summary>
        /// Gets result of casting a ray from gesture pointers centroid screen position.
        /// </summary>
        /// <returns> <c>true</c> if ray hits gesture's target; <c>false</c> otherwise. </returns>
        public bool GetTargetHitResult()
        {
            TouchHit hit;
            return GetTargetHitResult(ScreenPosition, out hit);
        }

        /// <summary>
        /// Gets result of casting a ray from gesture pointers centroid screen position.
        /// </summary>
        /// <param name="hit"> Raycast result </param>
        /// <returns> <c>true</c> if ray hits gesture's target; <c>false</c> otherwise. </returns>
        public virtual bool GetTargetHitResult(out TouchHit hit)
        {
            return GetTargetHitResult(ScreenPosition, out hit);
        }

        /// <summary>
        /// Gets result of casting a ray from specific screen position.
        /// </summary>
        /// <param name="position"> The position. </param>
        /// <returns> <c>true</c> if ray hits gesture's target; <c>false</c> otherwise. </returns>
        public bool GetTargetHitResult(Vector2 position)
        {
            TouchHit hit;
            return GetTargetHitResult(position, out hit);
        }

        /// <summary>
        /// Gets result of casting a ray from specific screen position.
        /// </summary>
        /// <param name="position"> The position. </param>
        /// <param name="hit"> Raycast result. </param>
        /// <returns> <c>true</c> if ray hits gesture's target; <c>false</c> otherwise. </returns>
        public virtual bool GetTargetHitResult(Vector2 position, out TouchHit hit)
        {
            if (layer != null)
            {
                if (layer.Hit(position, out hit) != TouchLayer.LayerHitResult.Hit) return false;
            }
            else
            {
                TouchLayer l = null;
                if (!touchManager.GetHitTarget(position, out hit, out l)) return false;
            }

            if (cachedTransform == hit.Target || hit.Target.IsChildOf(cachedTransform)) return true;
            return false;
        }

        /// <summary>
        /// Determines whether gesture controls a pointer.
        /// </summary>
        /// <param name="pointer"> The pointer. </param>
        /// <returns> <c>true</c> if gesture controls the pointer point; <c>false</c> otherwise. </returns>
        public bool HasPointer(Pointer pointer)
        {
            return activePointers.Contains(pointer);
        }

        /// <summary>
        /// Determines whether this instance can prevent the specified gesture.
        /// </summary>
        /// <param name="gesture"> The gesture. </param>
        /// <returns> <c>true</c> if this instance can prevent the specified gesture; <c>false</c> otherwise. </returns>
        public virtual bool CanPreventGesture(Gesture gesture)
        {
            if (Delegate == null)
            {
                if (gesture.CanBePreventedByGesture(this)) return !IsFriendly(gesture);
                return false;
            }
            return !Delegate.ShouldRecognizeSimultaneously(this, gesture);
        }

        /// <summary>
        /// Determines whether this instance can be prevented by specified gesture.
        /// </summary>
        /// <param name="gesture"> The gesture. </param>
        /// <returns> <c>true</c> if this instance can be prevented by specified gesture; <c>false</c> otherwise. </returns>
        public virtual bool CanBePreventedByGesture(Gesture gesture)
        {
            if (Delegate == null) return !IsFriendly(gesture);
            return !Delegate.ShouldRecognizeSimultaneously(this, gesture);
        }

        /// <summary>
        /// Specifies if gesture can receive this specific pointer point.
        /// </summary>
        /// <param name="pointer"> The pointer. </param>
        /// <returns> <c>true</c> if this pointer should be received by the gesture; <c>false</c> otherwise. </returns>
        public virtual bool ShouldReceivePointer(Pointer pointer)
        {
            if (Delegate == null) return true;
            return Delegate.ShouldReceivePointer(this, pointer);
        }

        /// <summary>
        /// Specifies if gesture can begin or recognize.
        /// </summary>
        /// <returns> <c>true</c> if gesture should begin; <c>false</c> otherwise. </returns>
        public virtual bool ShouldBegin()
        {
            if (Delegate == null) return true;
            return Delegate.ShouldBegin(this);
        }

        /// <summary>
        /// Cancels this gesture.
        /// </summary>
        /// <param name="cancelPointers"> if set to <c>true</c> also implicitly cancels all pointers owned by the gesture. </param>
        /// <param name="returnPointers"> if set to <c>true</c> redispatched all canceled pointers. </param>
        public void Cancel(bool cancelPointers, bool returnPointers)
        {
            switch (state)
            {
                case GestureState.Cancelled:
                case GestureState.Ended:
                case GestureState.Failed:
                    return;
            }

            setState(GestureState.Cancelled);

            if (!cancelPointers) return;
            for (var i = 0; i < numPointers; i++) touchManager.CancelPointer(activePointers[i].Id, returnPointers);
        }

        /// <summary>
        /// Cancels this gesture.
        /// </summary>
        public void Cancel()
        {
            Cancel(false, false);
        }

        #endregion

        #region Unity methods

        /// <inheritdoc />
        protected virtual void Awake()
        {
            cachedTransform = transform;

            var count = friendlyGestures.Count;
            for (var i = 0; i < count; i++)
            {
                AddFriendlyGesture(friendlyGestures[i]);
            }
            RequireGestureToFail = requireGestureToFail;
        }

        /// <summary>
        /// Unity3d Start handler.
        /// </summary>
        protected virtual void OnEnable()
        {
            // TouchManager might be different in another scene
            touchManager = TouchManager.Instance;
            gestureManagerInstance = GestureManager.Instance as GestureManagerInstance;

            if (touchManager == null)
                Debug.LogError("No TouchManager found! Please add an instance of TouchManager to the scene!");
            if (gestureManagerInstance == null)
                Debug.LogError("No GesturehManager found! Please add an instance of GesturehManager to the scene!");

            if (sendMessageTarget == null) sendMessageTarget = gameObject;
            INTERNAL_Reset();
        }

        /// <summary>
        /// Unity3d OnDisable handler.
        /// </summary>
        protected virtual void OnDisable()
        {
            setState(GestureState.Cancelled);
        }

        /// <summary>
        /// Unity3d OnDestroy handler.
        /// </summary>
        protected virtual void OnDestroy()
        {
            var copy = new List<Gesture>(friendlyGestures);
            var count = copy.Count;
            for (var i = 0; i < count; i++)
            {
                INTERNAL_RemoveFriendlyGesture(copy[i]);
            }
            RequireGestureToFail = null;
        }

        #endregion

        #region Internal functions

        internal void INTERNAL_SetState(GestureState value)
        {
            setState(value);
        }

        internal void INTERNAL_Reset()
        {
            activePointers.Clear();
            numPointers = 0;
            delayedStateChange = GestureState.Possible;
            pointersNumState = PointersNumState.TooFew;
            requiredGestureFailed = false;
            reset();
        }

        internal void INTERNAL_PointersPressed(IList<Pointer> pointers)
        {
            if (numPointers == 0) layer = pointers[0].Layer;

            var count = pointers.Count;
            var total = numPointers + count;
            pointersNumState = PointersNumState.InRange;

            if (minPointers <= 0)
            {
                // MinPointers is not set and we got our first pointers
                if (numPointers == 0) pointersNumState = PointersNumState.PassedMinThreshold;
            }
            else
            {
                if (numPointers < minPointers)
                {
                    // had < MinPointers, got >= MinPointers
                    if (total >= minPointers) pointersNumState = PointersNumState.PassedMinThreshold;
                    else pointersNumState = PointersNumState.TooFew;
                }
            }

            if (maxPointers > 0)
            {
                if (numPointers <= maxPointers)
                {
                    if (total > maxPointers)
                    {
                        // this event we crossed both MinPointers and MaxPointers
                        if (pointersNumState == PointersNumState.PassedMinThreshold) pointersNumState = PointersNumState.PassedMinMaxThreshold;
                        // this event we crossed MaxPointers
                        else pointersNumState = PointersNumState.PassedMaxThreshold;
                    }
                }
                // last event we already were over MaxPointers
                else pointersNumState = PointersNumState.TooMany;
            }

            if (state == GestureState.Began || state == GestureState.Changed)
            {
                for (var i = 0; i < count; i++) pointers[i].INTERNAL_Retain();
            }

            activePointers.AddRange(pointers);
            numPointers = total;
            pointersPressed(pointers);
        }

        internal void INTERNAL_PointersUpdated(IList<Pointer> pointers)
        {
            pointersNumState = PointersNumState.InRange;
            if (minPointers > 0 && numPointers < minPointers) pointersNumState = PointersNumState.TooFew;
            if (maxPointers > 0 && pointersNumState == PointersNumState.InRange && numPointers > maxPointers) pointersNumState = PointersNumState.TooMany;
            pointersUpdated(pointers);
        }

        internal void INTERNAL_PointersReleased(IList<Pointer> pointers)
        {
            var count = pointers.Count;
            var total = numPointers - count;
            pointersNumState = PointersNumState.InRange;

            if (minPointers <= 0)
            {
                // have no pointers
                if (total == 0) pointersNumState = PointersNumState.PassedMinThreshold;
            }
            else
            {
                if (numPointers >= minPointers)
                {
                    // had >= MinPointers, got < MinPointers
                    if (total < minPointers) pointersNumState = PointersNumState.PassedMinThreshold;
                }
                // last event we already were under MinPointers
                else pointersNumState = PointersNumState.TooFew;
            }

            if (maxPointers > 0)
            {
                if (numPointers > maxPointers)
                {
                    if (total <= maxPointers)
                    {
                        // this event we crossed both MinPointers and MaxPointers
                        if (pointersNumState == PointersNumState.PassedMinThreshold) pointersNumState = PointersNumState.PassedMinMaxThreshold;
                        // this event we crossed MaxPointers
                        else pointersNumState = PointersNumState.PassedMaxThreshold;
                    }
                    // last event we already were over MaxPointers
                    else pointersNumState = PointersNumState.TooMany;
                }
            }

            for (var i = 0; i < count; i++) activePointers.Remove(pointers[i]);
            numPointers = total;

            if (combinePointers)
            {
                for (var i = 0; i < count; i++) pointerSequence.Add(pointers[i]);

                if (NumPointers == 0)
                {
                    // Checking which points were removed in clusterExistenceTime seconds to set their centroid as cached screen position
                    var cluster = pointerSequence.FindElementsLaterThan(Time.time - combinePointersInterval,
                        shouldCachePointerPosition);
                    cachedScreenPosition = ClusterUtils.Get2DCenterPosition(cluster);
                    cachedPreviousScreenPosition = ClusterUtils.GetPrevious2DCenterPosition(cluster);
                }
            }
            else
            {
                if (NumPointers == 0)
                {
                    var lastPoint = pointers[count - 1];
                    if (shouldCachePointerPosition(lastPoint))
                    {
                        cachedScreenPosition = lastPoint.Position;
                        cachedPreviousScreenPosition = lastPoint.PreviousPosition;
                    }
                    else
                    {
                        cachedScreenPosition = TouchManager.INVALID_POSITION;
                        cachedPreviousScreenPosition = TouchManager.INVALID_POSITION;
                    }
                }
            }

            pointersReleased(pointers);
        }

        internal void INTERNAL_PointersCancelled(IList<Pointer> pointers)
        {
            var count = pointers.Count;
            var total = numPointers - count;
            pointersNumState = PointersNumState.InRange;

            if (minPointers <= 0)
            {
                // have no pointers
                if (total == 0) pointersNumState = PointersNumState.PassedMinThreshold;
            }
            else
            {
                if (numPointers >= minPointers)
                {
                    // had >= MinPointers, got < MinPointers
                    if (total < minPointers) pointersNumState = PointersNumState.PassedMinThreshold;
                }
                // last event we already were under MinPointers
                else pointersNumState = PointersNumState.TooFew;
            }

            if (maxPointers > 0)
            {
                if (numPointers > maxPointers)
                {
                    if (total <= maxPointers)
                    {
                        // this event we crossed both MinPointers and MaxPointers
                        if (pointersNumState == PointersNumState.PassedMinThreshold) pointersNumState = PointersNumState.PassedMinMaxThreshold;
                        // this event we crossed MaxPointers
                        else pointersNumState = PointersNumState.PassedMaxThreshold;
                    }
                    // last event we already were over MaxPointers
                    else pointersNumState = PointersNumState.TooMany;
                }
            }

            for (var i = 0; i < count; i++) activePointers.Remove(pointers[i]);
            numPointers = total;
            pointersCancelled(pointers);
        }

        internal virtual void INTERNAL_RemoveFriendlyGesture(Gesture gesture)
        {
            if (gesture == null || gesture == this) return;

            unregisterFriendlyGesture(gesture);
            gesture.unregisterFriendlyGesture(this);
        }

        #endregion

        #region Protected methods

        /// <summary>
        /// Should the gesture cache this pointers to use it later in calculation of <see cref="ScreenPosition"/>.
        /// </summary>
        /// <param name="value"> Pointer to cache. </param>
        /// <returns> <c>true</c> if pointers should be cached; <c>false</c> otherwise. </returns>
        protected virtual bool shouldCachePointerPosition(Pointer value)
        {
            return true;
        }

        /// <summary>
        /// Tries to change gesture state.
        /// </summary>
        /// <param name="value"> New state. </param>
        /// <returns> <c>true</c> if state was changed; otherwise, <c>false</c>. </returns>
        protected bool setState(GestureState value)
        {
            if (gestureManagerInstance == null) return false;
            if (requireGestureToFail != null)
            {
                switch (value)
                {
                    case GestureState.Recognized:
                    case GestureState.Began:
                        if (!requiredGestureFailed)
                        {
                            delayedStateChange = value;
                            return false;
                        }
                        break;
                    case GestureState.Possible:
                    case GestureState.Failed:
                    case GestureState.Cancelled:
                        delayedStateChange = GestureState.Possible;
                        break;
                }
            }

            var newState = gestureManagerInstance.INTERNAL_GestureChangeState(this, value);
            State = newState;

            return value == newState;
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// Called when new pointers appear.
        /// </summary>
        /// <param name="pointers"> The pointers. </param>
        protected virtual void pointersPressed(IList<Pointer> pointers) {}

        /// <summary>
        /// Called for moved pointers.
        /// </summary>
        /// <param name="pointers"> The pointers. </param>
        protected virtual void pointersUpdated(IList<Pointer> pointers) {}

        /// <summary>
        /// Called if pointers are removed.
        /// </summary>
        /// <param name="pointers"> The pointers. </param>
        protected virtual void pointersReleased(IList<Pointer> pointers) {}

        /// <summary>
        /// Called when pointers are cancelled.
        /// </summary>
        /// <param name="pointers"> The pointers. </param>
        protected virtual void pointersCancelled(IList<Pointer> pointers)
        {
            if (pointersNumState == PointersNumState.PassedMinThreshold)
            {
                // moved below the threshold
                switch (state)
                {
                    case GestureState.Began:
                    case GestureState.Changed:
                        // cancel started gestures
                        setState(GestureState.Cancelled);
                        break;
                }
            }
        }

        /// <summary>
        /// Called to reset gesture state after it fails or recognizes.
        /// </summary>
        protected virtual void reset()
        {
            layer = null;
            cachedScreenPosition = TouchManager.INVALID_POSITION;
            cachedPreviousScreenPosition = TouchManager.INVALID_POSITION;
        }

        /// <summary>
        /// Called when state is changed to Possible.
        /// </summary>
        protected virtual void onPossible() {}

        /// <summary>
        /// Called when state is changed to Began.
        /// </summary>
        protected virtual void onBegan() {}

        /// <summary>
        /// Called when state is changed to Changed.
        /// </summary>
        protected virtual void onChanged() {}

        /// <summary>
        /// Called when state is changed to Recognized.
        /// </summary>
        protected virtual void onRecognized() {}

        /// <summary>
        /// Called when state is changed to Failed.
        /// </summary>
        protected virtual void onFailed() {}

        /// <summary>
        /// Called when state is changed to Cancelled.
        /// </summary>
        protected virtual void onCancelled()
        {
            if (cancelledInvoker != null) cancelledInvoker.InvokeHandleExceptions(this, EventArgs.Empty);
            if (useSendMessage && SendMessageTarget != null)
                sendMessageTarget.SendMessage(CANCEL_MESSAGE, this, SendMessageOptions.DontRequireReceiver);
        }

        #endregion

        #region Private functions

        private void retainPointers()
        {
            var total = NumPointers;
            for (var i = 0; i < total; i++) activePointers[i].INTERNAL_Retain();
        }

        private void releasePointers(bool cancel)
        {
            var total = NumPointers;
            for (var i = 0; i < total; i++)
            {
                var pointer = activePointers[i];
                if (pointer.INTERNAL_Release() == 0 && cancel) touchManager.CancelPointer(pointer.Id, true);
            }
        }

        private void registerFriendlyGesture(Gesture gesture)
        {
            if (gesture == null || gesture == this) return;

            if (!friendlyGestures.Contains(gesture)) friendlyGestures.Add(gesture);
        }

        private void unregisterFriendlyGesture(Gesture gesture)
        {
            if (gesture == null || gesture == this) return;

            friendlyGestures.Remove(gesture);
        }

        #endregion

        #region Event handlers

        private void requiredToFailGestureStateChangedHandler(object sender, GestureStateChangeEventArgs e)
        {
            if ((sender as Gesture) != requireGestureToFail) return;
            switch (e.State)
            {
                case GestureState.Failed:
                    requiredGestureFailed = true;
                    if (delayedStateChange != GestureState.Possible)
                    {
                        setState(delayedStateChange);
                    }
                    break;
                case GestureState.Began:
                case GestureState.Recognized:
                case GestureState.Cancelled:
                    if (state != GestureState.Failed) setState(GestureState.Failed);
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for Gesture events
    /// </summary>
    public class GestureStateChangeEventArgs : EventArgs
    {
        /// <summary>
        /// Previous gesture state.
        /// </summary>
        public Gesture.GestureState PreviousState { get; private set; }

        /// <summary>
        /// Current gesture state.
        /// </summary>
        public Gesture.GestureState State { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GestureStateChangeEventArgs"/> class.
        /// </summary>
        /// <param name="state"> Current gesture state. </param>
        /// <param name="previousState"> Previous gesture state. </param>
        public GestureStateChangeEventArgs(Gesture.GestureState state, Gesture.GestureState previousState)
        {
            State = state;
            PreviousState = previousState;
        }
    }
}