using UnityEngine;
using ThreeDPool.EventHandlers;
using ThreeDPool.Managers;

namespace ThreeDPool.Controllers
{
    public class CueBallController : MonoBehaviour
    {
        public enum CueBallType
        {
            White = 0,
            Yellow,
            Blue,
            Red,
            Purple,
            Orange,
            Green,
            Burgandy,
            Black,
            Striped_Yellow,
            Striped_Blue,
            Striped_Red,
            Striped_Purple,
            Striped_Orange,
            Striped_Green,
            Striped_Burgandy,
        }

        [SerializeField]
        float _force = 30f;

        [SerializeField]
        CueBallType _ballType = CueBallType.White;

        // keep track of the current event
        private CueBallActionEvent.States _currState;

        private Vector3 _initialPos;

        // flag that diffrentiates between the ball that is pocketed in the current turn versus ball that is pocketed in the earlier turn
        // this helps in scoring
        public bool IsPocketedInPrevTurn;

        public CueBallType BallType { get { return _ballType; } }


        private void Start()
        {
            // record the intial position so that they could be placed in their original position if it goes out of table
            _initialPos = transform.position;

            EventManager.Subscribe(typeof(CueBallActionEvent).Name, OnCueBallEvent);
            EventManager.Subscribe(typeof(GameStateEvent).Name, OnGameStateEvent);
        }

        private void OnDestroy()
        {
            EventManager.Unsubscribe(typeof(CueBallActionEvent).Name, OnCueBallEvent);
            EventManager.Unsubscribe(typeof(GameStateEvent).Name, OnGameStateEvent);
        }

        private void OnCueBallEvent(object sender, IGameEvent gameEvent)
        {
            CueBallActionEvent actionEvent = (CueBallActionEvent)gameEvent;
            switch(actionEvent.State)
            {
                case CueBallActionEvent.States.Stationary:
                    {
                        // change the curr state to default now
                        _currState = CueBallActionEvent.States.Default;
                    }
                    break;
            }
        }

        private void OnGameStateEvent(object sender, IGameEvent gameEvent)
        {
            GameStateEvent gameStateEvent = (GameStateEvent)gameEvent;
            switch (gameStateEvent.GameState)
            {
                case GameStateEvent.State.Play:
                    {
                        PlaceBallInInitialPos();
                    }
                    break;
            }
        }

        private void OnTriggerEnter(Collider collider)
        {
            CueController cueController = collider.gameObject.transform.parent.GetComponent<CueController>();

            // confirm if the ball is actually hit by a ball 
            if (cueController != null)
            {
                // ball is hit by the cue
                if (_ballType == CueBallType.White)
                {
                    // notify that the ball is hit
                    EventManager.Notify(typeof(CueBallActionEvent).Name, this, new CueBallActionEvent() { State = CueBallActionEvent.States.Striked });

                    // set the current state
                    _currState = CueBallActionEvent.States.Striked;

                    // whats the force gathered to hit
                    float forceGatheredToHit = cueController.ForceGatheredToHit;

                    // set the ball rolling with gathered force
                    OnStriked(forceGatheredToHit);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Floor"))
            {
                Debug.Log("Oncollision" + collision.gameObject.name);

                // potted information is detected by PocketCollider
                // if ball got potted,
                // check if the ball potted is a cue ball, if yes then player looses the point and the cue is placed back in the table
                // if not then its a point to the current player
                GameManager.Instance.AddToBallHitOutList(this);
            }
        }

        /// <summary>
        /// ball goes through various lifecycle from default, placing to the coming back to stationary state after been striked
        /// </summary>
        private void FixedUpdate()
        {
            Rigidbody rigidbody = gameObject.GetComponent<Rigidbody>();
            if ((_currState == CueBallActionEvent.States.Placing) && rigidbody.IsSleeping())
            {
                _currState = CueBallActionEvent.States.Default;
            }
            else if ((_currState == CueBallActionEvent.States.Default) && (!rigidbody.IsSleeping()))
            {
                // number of balls striked in Play mode
                if (GameManager.Instance.CurrGameState == GameManager.GameState.Play)
                    GameManager.Instance.NumOfBallsStriked++;

                _currState = CueBallActionEvent.States.Striked;
            }
            else if ((_currState == CueBallActionEvent.States.Striked) && (!rigidbody.IsSleeping()))
            {
                _currState = CueBallActionEvent.States.InMotion;
            }
            else if ((_currState == CueBallActionEvent.States.Striked) && (rigidbody.IsSleeping()))
            {
                _currState = CueBallActionEvent.States.InMotion;
            }
            else if ((_currState == CueBallActionEvent.States.InMotion) && rigidbody.IsSleeping())
            {
                // ball is come to rest after been striked, check for the status
                GameManager.Instance.ReadyForNextRound();

                _currState = CueBallActionEvent.States.Stationary;
            }
            else
            {
                // do nothing
            }
        }

        /// <summary>
        /// OnStriked by Cue
        /// </summary>
        /// <param name="forceGathered">amount of force applied on the cue ball</param>
        private void OnStriked(float forceGathered)
        {
            // only apply force on the white ball
            if (_ballType == CueBallType.White)
            {
                GameManager.Instance.NumOfBallsStriked++;

                Rigidbody rigidBody = gameObject.GetComponent<Rigidbody>();
                rigidBody.AddForce(Camera.main.transform.forward * _force * forceGathered, ForceMode.Force);
            }
        }

        /// <summary>
        /// this function is called by the pockets collider in the pool table when a cue ball touches it
        /// </summary>
        public void BallPocketed()
        {
            GameManager.Instance.AddToBallPocketedList(this);
        }

        /// <summary>
        /// this function is called only while in practise mode
        /// </summary>
        public void PlaceBallInPosWhilePractise()
        {
            PlaceBallInInitialPos();

            EventManager.Notify(typeof(CueBallActionEvent).Name, this, new CueBallActionEvent() { State = CueBallActionEvent.States.Stationary });
        }

        /// <summary>
        /// this function is been called whenever there is a reset of p
        /// </summary>
        public void PlaceBallInInitialPos()
        {
            // place it a bit from top so that balls dont get placed within each other
            transform.position = new Vector3(_initialPos.x, _initialPos.y + 0.2f, _initialPos.z);

            // this flag resets after the completion of one complete round,
            // since the function is reused, and whenever there is a reset, it is safe to reset the flag here
            IsPocketedInPrevTurn = false;

            _currState = CueBallActionEvent.States.Placing;
            GameManager.Instance.NumOfBallsStriked = 0;
        }
    }
}
