using System.Collections;
using System.Collections.Generic;
using DDQ.Model;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SafePuzzle : Puzzle
{
    [Header("Gameplay")]
    [SerializeField] private float _zoomedOutFov;
    [SerializeField] private float _zoomMiddleFov;
    [SerializeField] private float _zoomInFov;
    [SerializeField] private float _zoomedOutBound = 5;
    [SerializeField] private float _zoomMiddleBound = 30;
    [SerializeField] private float _holdTimeForCompletetion = 1f;
    [SerializeField] private SphereCollider _dialCollider;
    [SerializeField] private Transform _rotatingParent;
    [SerializeField] private float _sensitivity = 0.4f;
    [SerializeField] private float _distBetweenDialClicks = 5;
    [SerializeField] private Image[] _correctNumberLocks;
    [SerializeField] private GameObject[] _sideBars;
    [SerializeField] private float _innerCollisionRadius = 35;


   [Header("Animation")]
    [SerializeField] private Transform _oldDial;
    [SerializeField] private Animation _animation;
    [SerializeField] private float _wrongRotationBufferRoom = 3;
    private bool _isAnimatingDialBounce = false;
    private List<int> _code = new List<int>();

    private Camera _mainCam;
    private bool _isRotating = false;
    private float _initialAngle;
    private float _oldZRot = 0;
    private float _initialInputAngle;
    private bool _canDooMiddleZoom = true;
    private enum _camerState { In, Middle, Out };
    private _camerState _camState = _camerState.Out;
    private int _codesComplete = 0;
    private bool _safeOpen = false;
    public static event PlayOneShotSound OnCorrectDialRotationSFX;
    public static event PlayOneShotSound OnDialRotationSFX;
    public static event PlayOneShotSound OnDialRotationCloseToCorrectSFX;

    public override bool UseTimer => true;

    public override void Configure(Vector2Int coord)
    {
        _dialCollider.enabled = false;
        Top.Instance.TryGetSceneRoot(out DungeonSceneRoot sceneRoot);
        sceneRoot.TryGetSubSceneRoot(out Chamber3DSubSceneRoot chamber3d);
        _mainCam = chamber3d.PlayerCamera.Camera;
        GenerateCode();
        base.Configure(coord);
    }

    private void GenerateCode()
    {
        int codeAmount = 0;
        //switch (DungeonRun.Current.Difficulty)
        switch (Difficulty.VeryEasy)
        {
            case Difficulty.VeryEasy:
                codeAmount = 3;
                break;
            case Difficulty.Easy:
                codeAmount = 3;
                break;
            case Difficulty.Medium:
                codeAmount = 4;
                break;
            case Difficulty.Hard:
                codeAmount = 5;
                break;
            case Difficulty.VeryHard:
                codeAmount = 5;
                break;
        }

        for (int i = 0; i < codeAmount; i++)
        {
            _sideBars[i].SetActive(true);
        }

        List<int> randNumbers = new List<int>();
        for (int i = 0; i < 360; i++)
        {
            randNumbers.Add(i);
        }

        randNumbers.Shuffle();

        float first = 30f;
        float minDist = 60f;
        for (int i = 0; i < codeAmount; i++)
        {
            float next = Mathf.Repeat(first + minDist + MNToolbox.Random.Next(60, true), 359);
            _code.Add((int)next);
            first = next;
        }
    }

    public override void Interact()
    {
        _dialCollider.enabled = true;
        base.Interact();
    }

    public override bool IsPuzzleComplete()
    {
        if(_codesComplete >= _code.Count) 
        {
            _safeOpen = true;
            OpenSafe();
            return true;
        }
        return false;
    }

    private bool _canLerpFOV = false;
    public void AdjustCamFovByDistanceOfNumber(float number)
    {
        float dist = _rotatingParent.eulerAngles.z - number;

        if(Mathf.Abs(dist) <= 30)
        {
            if (_camState != _camerState.Middle)
            {
                if (_canDooMiddleZoom)
                {
                    _camState = _camerState.Middle;
                    LeanTween.cancel(gameObject);
                    LeanTween.value(gameObject, _mainCam.fieldOfView, _zoomMiddleFov, 0.5f).setOnUpdate((float value) =>
                    {
                        _mainCam.fieldOfView = value;

                    }).setOnComplete(() =>
                    {
                        _canLerpFOV = true;
                    });


                }
                else
                {
                    _camState = _camerState.Out;
                    LeanTween.cancel(gameObject);
                    LeanTween.value(gameObject, _mainCam.fieldOfView, _zoomedOutFov, 0.2f).setOnUpdate((float value) =>
                    {
                        _mainCam.fieldOfView = value;
                    });
                    _canLerpFOV = false;
                }

            }

            if (_canLerpFOV && _canDooMiddleZoom)
            {
                float l = (Mathf.Abs(dist) / 30);
                float fov = Mathf.Lerp(_zoomInFov, _zoomMiddleFov, Mathf.Abs(l));
                if (_mainCam.fieldOfView != fov)
                {
                    LeanTween.cancel(gameObject);
                    LeanTween.value(gameObject, _mainCam.fieldOfView, fov, 0.5f).setOnUpdate((float value) =>
                    {
                        _mainCam.fieldOfView = value;
                    });
                }

                if (Mathf.Abs(dist) <= 5)
                {
                    LeanTween.delayedCall(gameObject, _holdTimeForCompletetion, () =>
                    {
                        GameObject sidebar = _sideBars[_codesComplete];
                        LeanTween.moveLocalX(sidebar, -0.2f, 1).setOnComplete(() =>
                        {
                            sidebar.SetActive(false);
                        });
                        Image tick = _correctNumberLocks[_codesComplete];
                        tick.color = new Color(1, 1, 1, 0.2f);
                        LeanTween.moveLocalY(tick.gameObject, tick.transform.localPosition.y + 35f, 0.2f).setLoopPingPong(1);
                        _codesComplete++;
                        OnCorrectDialRotationSFX?.Invoke();
                        EvaluatePuzzleCompletion();
                    });
                }

            }
        }
        else
        {
            _canLerpFOV = false;
            if (_camState != _camerState.Out)
            {
                _camState = _camerState.Out;
                LeanTween.cancel(gameObject);
                LeanTween.value(gameObject, _mainCam.fieldOfView, _zoomedOutFov, 0.5f).setOnUpdate((float value) =>
                {
                    _mainCam.fieldOfView = value;
                });
            }
        }
    }

    private void OpenSafe()
    {
        _oldDial.localEulerAngles = new Vector3(_oldDial.localEulerAngles.x, (_rotatingParent.localEulerAngles.z * -1), _oldDial.localEulerAngles.z);
        _oldDial.gameObject.SetActive(true);
        _animation.Play();
        _rotatingParent.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (IsPuzzleActive())
        {

            if (InputHandler.GetMouseActionButtonDown()
                && PlayerInputHandler.OnMouseRaycastUI(UIPuzzleCanvas.Canvas) == null
                && InputHandler.OnMouseRaycast(Chamber3DSubSceneRoot.DefaultMask) == _dialCollider.gameObject)
            {
                _isRotating = true;
                Vector3 dir = Input.mousePosition - (_mainCam.WorldToScreenPoint(_rotatingParent.position));
                dir.Normalize();
                _initialInputAngle = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                _initialAngle = _rotatingParent.localEulerAngles.z;
            }

            if (InputHandler.GetMouseActionButtonUp())
            {
                _isRotating = false;
            }

            if (InputHandler.GetMouseActionButtonHoldDown()
                 && InputHandler.OnMouseRaycast(Chamber3DSubSceneRoot.DefaultMask) == _dialCollider.gameObject)
            {
                if(!_isAnimatingDialBounce)
                {
                    if (_isRotating)
                    {
                        //make sure the mopus epointer is bigger tyhan the radius in the middle 
                        float dis = Vector3.Distance(Input.mousePosition, _mainCam.WorldToScreenPoint(_dialCollider.transform.position));
                        //float dis = Vector3.Distance(Input.mousePosition, _dialCollider.transform.position);
                        if(dis >= _innerCollisionRadius)
                        {                   
                            Vector3 dir = Input.mousePosition - (_mainCam.WorldToScreenPoint(_rotatingParent.position));
                            dir.Normalize();
                            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                            angle = _initialAngle + (_initialInputAngle - angle);                   
                            if (_codesComplete % 2 == 0)
                            {
                                if (_rotatingParent.localEulerAngles.z < angle)
                                {
                                    if (!_isAnimatingDialBounce)
                                    {
                                        _rotatingParent.localEulerAngles = Quaternion.AngleAxis(angle, Vector3.forward).eulerAngles;
                                    }

                                }
                                else if (_rotatingParent.localEulerAngles.z > Mathf.Abs( angle) + _wrongRotationBufferRoom)
                                {
                                    //play a little bounce 
                                    _isRotating = false;
                                    _isAnimatingDialBounce = true;
                                    LeanTween.rotateLocal(_rotatingParent.gameObject, new Vector3(_rotatingParent.localEulerAngles.x, _rotatingParent.localEulerAngles.y, _rotatingParent.localEulerAngles.z - 5), 0.2f).setOnComplete(() =>
                                    {
                                        _isAnimatingDialBounce = false;
                                    }).setLoopPingPong(1);
                                }
                            }
                            else
                            {
                                if (_rotatingParent.localEulerAngles.z > angle)
                                {
                                    if (!_isAnimatingDialBounce)
                                    {
                                        _rotatingParent.localEulerAngles = Quaternion.AngleAxis(angle, Vector3.forward).eulerAngles;
                                    }
                                }
                                else if (_rotatingParent.localEulerAngles.z < Mathf.Abs(angle) - _wrongRotationBufferRoom)
                                {
                                    //play a little bounce 
                                    _isRotating = false;
                                    _isAnimatingDialBounce = true;
                                    LeanTween.rotateLocal(_rotatingParent.gameObject, new Vector3(_rotatingParent.localEulerAngles.x, _rotatingParent.localEulerAngles.y, _rotatingParent.localEulerAngles.z + 5), 0.2f).setOnComplete(() =>
                                    {
                                        _isAnimatingDialBounce = false;
                                    }).setLoopPingPong(1);
                                }
                            }

                            //change sound based on distance
                            if (_oldZRot > (_rotatingParent.localEulerAngles.z + _distBetweenDialClicks)
                                || _oldZRot < (_rotatingParent.localEulerAngles.z - _distBetweenDialClicks))
                            {
                                _oldZRot = _rotatingParent.localEulerAngles.z;
                                if (_camState == _camerState.Middle || _camState == _camerState.In)
                                {
                                    OnDialRotationCloseToCorrectSFX?.Invoke();
                                }
                                else
                                {
                                    OnDialRotationSFX?.Invoke();
                                }
                            }

                            //can u do middle zoom 
                            if (_codesComplete % 2 == 0)
                            {
                                //going right
                                //if less than the number zoom out 
                                if (_rotatingParent.localEulerAngles.z > _code[_codesComplete])
                                {
                                    //no middle zoom
                                    _canDooMiddleZoom = false;
                                }
                                else
                                {
                                    _canDooMiddleZoom = true;
                                }
                            }
                            else
                            {
                                //going left 
                                //if more than the number zoom out 
                                if (_rotatingParent.localEulerAngles.z < _code[_codesComplete])
                                {
                                    //no middle zoom
                                    _canDooMiddleZoom = false;
                                }
                                else
                                {
                                    _canDooMiddleZoom = true;
                                }
                            }

                        }
                        else
                        {
                            _isRotating = false;
                        }
                    }
                }              
            }
            else
            {
                _isRotating = false;
            }

            if (!_isAnimatingDialBounce && !_safeOpen)
            {
                AdjustCamFovByDistanceOfNumber(_code[_codesComplete]);
            }

            Debug.Log(_rotatingParent.localEulerAngles.z);
        }
    }
}
