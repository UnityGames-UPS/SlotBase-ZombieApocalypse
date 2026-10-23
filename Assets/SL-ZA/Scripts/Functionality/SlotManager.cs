using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using System;
using System.Collections;
using Spine.Unity;
using Unity.VisualScripting;

public class SlotManager : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite[] _symbolSprites;
    [SerializeField] private List<AnimationSprites> symbolAnimations;

    [Header("Slot Images")]
    [SerializeField] private List<SlotImage> _totalImages;
    [SerializeField] internal List<SlotImage> _resultImages;
    [SerializeField] private List<ImageAnimation> reelBgs;
    [SerializeField] private List<SlotImage> winAnimationImages;
    [SerializeField] internal List<SlotImage> SlotOverlays;
    [SerializeField] internal List<SlotImage> WinFrames;

    [Header("Slots Transforms")]
    [SerializeField] private Transform[] _slotTransforms;
    [SerializeField] private RectTransform mainSlotTransform;

    [Header("Bgs")]
    [SerializeField] private GameObject landscapeBackground;
    [SerializeField] private GameObject portraitBackground;

    [Header("WinLine UI")]
    [SerializeField] private TMP_Text winAmountBigText;
    [SerializeField] private List<TMP_Text> lineWinTexts;

    [Header("Managers")]
    [SerializeField] private AudioController audioController;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private OrientationChange orientationChange;


    internal bool SocketConnected = false;
    internal bool _isAutoSpin = false;
    internal bool isInFreeSpins = false;
    internal bool isBonus = false;
    internal int freeSpinsRemaining = 0;
    private bool _wasAutoSpinOn;
    private List<Tween> _alltweens = new List<Tween>();
    private Coroutine _autoSpinRoutine = null;
    private Coroutine _tweenRoutine;
    private Coroutine _winLinesLoopRoutine;
    // Slots converted from symbolId 0 to Red Monopoly by a golden multiplier this spin —
    // consulted by the win-line highlight so those slots keep showing the red variant.
    private bool _isSpinning = false;
    internal int _numberOfSlots = 5;
    private bool _stopSpinToggle;
    private float _spinDelay = 0.3f;
    private bool _isTurboOn;
    internal bool IsTurboOn => _isTurboOn;
    private bool isTweening = false;

    internal enum SpinSpeed { Normal, Turbo, QuickSpin }
    internal SpinSpeed currentSpinSpeed = SpinSpeed.Normal;

    internal void SetSpinSpeed(SpinSpeed speed)
    {
        currentSpinSpeed = speed;
        _isTurboOn = (speed != SpinSpeed.Normal);
        _spinDelay = speed switch
        {
            SpinSpeed.Normal => 0.3f,
            SpinSpeed.Turbo => 0.2f,
            SpinSpeed.QuickSpin => 0.05f,
            _ => 0.3f
        };
    }

    #region Initial Functions

    private void Start()
    {
        shuffleSlotImages();

        SetupSymbolClickHandlers();

    }

    private void SetupSymbolClickHandlers()
    {
        if (uiManager == null || _resultImages == null) return;

        for (int col = 0; col < _resultImages.Count; col++)
        {
            for (int row = 0; row < _resultImages[col].slotImages.Count; row++)
            {
                Image symbolImage = _resultImages[col].slotImages[row];
                if (symbolImage == null) continue;

                SymbolClickHandler handler = symbolImage.GetComponent<SymbolClickHandler>();
                if (handler == null) handler = symbolImage.gameObject.AddComponent<SymbolClickHandler>();
                handler.Init(col, row, this, uiManager);
            }
        }
    }

    internal void shuffleSlotImages(bool midTween = false)
    {
        foreach (var slotImg in _totalImages)
        {
            for (int j = 0; j < slotImg.slotImages.Count; j++)
            {
                int randomSprite = UnityEngine.Random.Range(0, 9);
                // Sprite image = (j % 2 == 0)
                //     ? _symbolSprites[randomSprite] // random symbol
                //     : _symbolSprites[0]; // blank

                Sprite image = _symbolSprites[randomSprite];
                slotImg.slotImages[j].sprite = image;
                slotImg.slotImages[j].preserveAspect = true;
                //SetSymbolSize(slotImg.slotImages[j], randomSprite);
            }
        }
    }

    #endregion

    #region Autospin
    internal int autoSpinRoundsRemaining = -1;

    internal void AutoSpin()
    {
        if (!_isAutoSpin)
        {
            _isAutoSpin = true;

            if (_autoSpinRoutine != null)
            {
                StopCoroutine(_autoSpinRoutine);
                _autoSpinRoutine = null;
            }
            _autoSpinRoutine = StartCoroutine(AutoSpinCoroutine());
        }
    }

    internal void AutoSpin(int rounds)
    {
        autoSpinRoundsRemaining = rounds;
        AutoSpin();
    }

    internal void StopAutoSpin()
    {
        //_audioController.PlayButtonAudio();
        if (_isAutoSpin)
        {
            _isAutoSpin = false;
            _wasAutoSpinOn = false;
            autoSpinRoundsRemaining = -1;
            StartCoroutine(StopAutoSpinCoroutine());
        }
    }

    private IEnumerator AutoSpinCoroutine()
    {
        while (_isAutoSpin)
        {
            StartSlots(_isAutoSpin);
            yield return new WaitUntil(() => !_isSpinning);

            if (autoSpinRoundsRemaining > 0)
            {
                autoSpinRoundsRemaining--;
                uiManager.UpdateAutoPlayCount();
                if (autoSpinRoundsRemaining == 0)
                {
                    StopAutoSpin();
                    break;
                }
            }

            yield return new WaitForSeconds(_spinDelay);
        }
        if (_wasAutoSpinOn)
            _wasAutoSpinOn = false;
    }

    private IEnumerator StopAutoSpinCoroutine()
    {
        yield return new WaitUntil(() => !_isSpinning);
        _isAutoSpin = false;
        uiManager.SetAutoSpinButtonInteractable(true);
        if (_autoSpinRoutine != null)
        {
            StopCoroutine(_autoSpinRoutine);
            _autoSpinRoutine = null;
        }
        // The current spin just finished and autospin is now fully cancelled.
        // Tell UIManager to restore the spin button now that we are truly idle.
        uiManager.SetSpinButtonReady();
    }
    #endregion

    internal void RequestInstantStop()
    {
        if (isTweening)
        {
            _stopSpinToggle = true;
        }
    }

    private OrientationChange GetOrientationChange()
    {
        if (orientationChange != null) return orientationChange;
        orientationChange = FindFirstObjectByType<OrientationChange>();
        return orientationChange;
    }

    #region SlotSpin
    internal void StartSlots(bool autoSpin = false)
    {
        if (_isSpinning) return;

        if (!autoSpin)
        {
            if (_autoSpinRoutine != null)
            {
                StopCoroutine(_autoSpinRoutine);
                StopCoroutine(_tweenRoutine);
                _tweenRoutine = null;
                _autoSpinRoutine = null;
            }
        }
        _tweenRoutine = StartCoroutine(TweenRoutine());
    }

    private IEnumerator TweenRoutine()
    {
        _isSpinning = true;
        isBonus = false;

        StopWinLinesLoop();

        if (uiManager.currentBalance < uiManager.currentTotalBet)
        {
            StopAutoSpin();
            _isSpinning = false;
            yield return new WaitForSeconds(0.2f);
            uiManager.LowBalPopup();
            // OnSpinButtonPressed disabled spin/bet controls before this spin was rejected;
            // restore them so the player can lower the bet and spin again.
            uiManager.SetSpinButtonReady();
            uiManager.SetBetButtonsInteractable(true);
            yield break;
        }

        uiManager.UpdateBalance(uiManager.currentBalance - uiManager.currentTotalBet);
        uiManager.currentBalance = uiManager.currentBalance - uiManager.currentTotalBet;
        uiManager.UpdateWin(0.00);
        yield return null;

        ResetOverlays();

        //if (_audioController) _audioController.PlayWLAudio("spin");

        _isSpinning = true;
        isTweening = true;

        for (int i = 0; i < _numberOfSlots; i++)
        {
            InitializeTweening(_slotTransforms[i]);
        }
        audioController.PlayReelSpinning(true);

        ResetAllSymbol();

        socketManager.AccumulateResult(uiManager.betCounter);
        yield return new WaitUntil(() => socketManager.isResultdone);

        // // Load result matrix into result images
        for (int j = 0; j < socketManager.resultData.matrix.Count; j++)
        {
            for (int i = 0; i < socketManager.resultData.matrix[j].Count; i++)
            {
                if (int.TryParse(socketManager.resultData.matrix[j][i], out int symbolId))
                {
                    _resultImages[i].slotImages[j].sprite = _symbolSprites[symbolId];
                    _resultImages[i].slotImages[j].preserveAspect = true;
                    //SetSymbolSize(_resultImages[i].slotImages[j], symbolId);
                }
            }
        }

        if (_isTurboOn)
        {
            _stopSpinToggle = true;
        }

        if (!_stopSpinToggle)
        {
            for (int i = 0; i < _numberOfSlots; i++)
            {
                yield return new WaitForSeconds(0.2f);
                if (_stopSpinToggle) break;
            }
        }

        for (int i = 0; i < _numberOfSlots; i++)
        {

            yield return StopTweening(_slotTransforms[i], i, _stopSpinToggle);

            audioController.PlayReelStop();
        }
        isTweening = false;
        _stopSpinToggle = false;

        yield return _alltweens[^1].WaitForCompletion();
        KillAllTweens();

        // Reels have fully stopped — nothing left to instant-stop, so the stop button goes away
        // right here rather than staying visible through any heatup/wheel-trigger animations.
        uiManager.HideStopButtonAfterReelsStopped();

        // Big/Huge/Mega win popup is shown before the win-line loop, which then plays once it's
        // closed (Take, or the 3s autoplay auto-close after Take becomes interactable).
        if (!isBonus)
        {
            var popupType = uiManager.GetWinPopupType(socketManager.resultData.payload.winAmount);
            if (popupType.HasValue)
            {
                bool popupClosed = false;
                uiManager.ShowUniversalWinPopup(popupType.Value, socketManager.resultData.payload.winAmount, _isAutoSpin, () => popupClosed = true);
                yield return new WaitUntil(() => popupClosed);
            }
            else if (socketManager.resultData.payload.winAmount > 0)
            {
                audioController.PlayNormalSmallWin();
            }
        }

        if (socketManager.resultData.payload.lineWins.Count > 0)
        {
            yield return ShowWinLineAnimation(socketManager.resultData.payload.lineWins, socketManager.resultData.payload.winAmount);
        }

        _isSpinning = false;
        uiManager.currentBalance = socketManager.resultData.player.balance;
        uiManager.SetSpinButtonReady();
    }
    #endregion

    #region Win Line Animation

    private IEnumerator ShowWinLineAnimation(List<LineWin> winLines, double winAmount, bool oneShot = false)
    {
        audioController.PlayWinLineIntro();
        uiManager.UpdateWin(winAmount, true);
        uiManager.UpdateBalance(uiManager.currentBalance + winAmount, true);

        if (_isAutoSpin || oneShot)
        {
            // Autospin: flash every winning line together once, then normally let
            // the next spin proceed — unless autoplay gets stopped while that's showing, in
            // which case switch to the manual-style per-line loop instead of silently ending.
            yield return ShowCombinedWinLinesHighlight(winLines);

            if (!oneShot && !_isAutoSpin)
            {
                _winLinesLoopRoutine = StartCoroutine(LoopWinLines(winLines));
            }
        }
        else
        {
            // Manual spin: keep cycling the winning lines until the player starts the next spin.
            yield return ShowCombinedWinLinesHighlight(winLines);
            _winLinesLoopRoutine = StartCoroutine(LoopWinLines(winLines));
        }
    }

    // Flashes every winning line together once — used both as the whole display for
    // autospin/free-spins (normally) and as the lead-in before LoopWinLines for manual spins.
    private IEnumerator ShowCombinedWinLinesHighlight(List<LineWin> winLines)
    {
        for (int j = 0; j < SlotOverlays.Count; j++)
        {
            for (int k = 0; k < SlotOverlays[j].slotImages.Count; k++)
            {
                SlotOverlays[j].slotImages[k].gameObject.SetActive(true);
                WinFrames[j].slotImages[k].gameObject.SetActive(false);
                //winAnimationImages[j].slotImages[k].GetComponent<ImageAnimation>().StopAnimation();
                //winAnimationImages[j].slotImages[k].gameObject.SetActive(false);
                SetResultImageVisible(_resultImages[j].slotImages[k], true);
            }
        }

        winAmountBigText.gameObject.SetActive(true);
        winAmountBigText.text = socketManager.resultData.payload.winAmount.ToString("F2");

        for (int j = 0; j < winLines.Count; j++)
        {
            foreach (var pos in winLines[j].matchedPositions)
            {
                // Locals keep the old naming: "col" = matrix row, "row" = reel index.
                int col = pos.row;
                int row = pos.col;
                //SlotOverlays[row].slotImages[col].gameObject.SetActive(false);
                int symbolID = int.Parse(socketManager.resultData.matrix[col][row]);

                if (symbolID != 0)
                {
                    WinFrames[row].slotImages[col].gameObject.SetActive(true);
                    //WinFrames[row].slotImages[col].GetComponent<ImageAnimation>().StartAnimation();
                }


                PlayWinSymbolAnimation(row, col, symbolID);
            }
        }

        yield return new WaitForSeconds(3f);
    }

    // Cycles the winning lines one at a time, forever. StopWinLinesLoop() (called from
    // TweenRoutine the moment a new spin starts) is what actually ends this.
    private IEnumerator LoopWinLines(List<LineWin> winLines)
    {
        winAmountBigText.gameObject.SetActive(false);
        // Cells (row, col) that were left highlighted/animating by the previously shown line.
        // Cells common to the previous line and the current one are left untouched below so
        // their WinFrames/winAnimationImages animation keeps playing instead of restarting.
        var previousPositions = new HashSet<(int row, int col)>();
        while (true)
        {
            for (int i = 0; i < winLines.Count; i++)
            {
                var currentPositions = new HashSet<(int row, int col)>();
                foreach (var pos in winLines[i].matchedPositions)
                {
                    currentPositions.Add((pos.col, pos.row));
                }

                for (int j = 0; j < SlotOverlays.Count; j++)
                {
                    for (int k = 0; k < SlotOverlays[j].slotImages.Count; k++)
                    {
                        // Still part of the highlighted set from the previous line and this one
                        // -> leave it fully alone so its animation keeps playing uninterrupted.
                        if (previousPositions.Contains((j, k)) && currentPositions.Contains((j, k)))
                            continue;

                        SlotOverlays[j].slotImages[k].gameObject.SetActive(true);
                        WinFrames[j].slotImages[k].gameObject.SetActive(false);
                        //winAnimationImages[j].slotImages[k].GetComponent<ImageAnimation>().StopAnimation();
                        SetResultImageVisible(_resultImages[j].slotImages[k], true);
                        //winAnimationImages[j].slotImages[k].gameObject.SetActive(false);
                    }
                }

                foreach (var pos in winLines[i].matchedPositions)
                {
                    int col = pos.row;
                    int row = pos.col;
                    int symbolID = int.Parse(socketManager.resultData.matrix[col][row]);
                    //SlotOverlays[row].slotImages[col].gameObject.SetActive(false);

                    // Already animating for the previous line and still part of this one ->
                    // skip re-activating/restarting it, let it keep animating as-is.
                    if (previousPositions.Contains((row, col)))
                    {
                        WinFrames[row].slotImages[col].GetComponent<ImageAnimation>().StartAnimation();
                        continue;
                    }

                    if (symbolID != 0)
                    {
                        WinFrames[row].slotImages[col].gameObject.SetActive(true);
                        WinFrames[row].slotImages[col].GetComponent<ImageAnimation>().StartAnimation();
                    }

                    PlayWinSymbolAnimation(row, col, symbolID);
                }
                audioController.PlayPaylineHighlight();

                foreach (var linewinText in lineWinTexts)
                {
                    linewinText.gameObject.SetActive(false);
                }
                int tempIndex = GetPerLineWinIndex(winLines[i].lineIndex);
                lineWinTexts[tempIndex].text = winLines[i].winInCash.ToString("F2");
                lineWinTexts[tempIndex].gameObject.SetActive(true);

                previousPositions = currentPositions;

                yield return new WaitForSeconds(2.4f);
            }
        }
    }

    // Shows the win-line animation for one slot, shared by ShowCombinedWinLinesHighlight and
    // LoopWinLines. A symbolId-0 slot that a golden multiplier turned into Red Monopoly this
    // spin (tracked in _goldenMonopolyPositions) plays MonopolyRedAnimation at MonopolyRedScale
    // instead of the normal symbol-0 win animation.
    private void PlayWinSymbolAnimation(int row, int col, int symbolID)
    {

        //winAnimationImages[row].slotImages[col].sprite = _symbolSprites[symbolID];

        //SetAnimationSymbolSize(winAnimationImages[row].slotImages[col], symbolID);

        //winAnimationImages[row].slotImages[col].preserveAspect = true;
        //winAnimationImages[row].slotImages[col].gameObject.SetActive(true);
        SetResultImageVisible(_resultImages[row].slotImages[col], false);

        // ImageAnimation anim = winAnimationImages[row].slotImages[col].GetComponent<ImageAnimation>();
        // anim.StopAnimation();
        // anim.textureArray = GetAnimationSprite(symbolID);
        // anim.AnimationSpeed = GetAnimationSpeed(symbolID);
        // anim.doLoopAnimation = true;
        // anim.StartAnimation();

    }

    /// <summary>
    /// Stops the repeating win-lines display (if running) and snaps its visuals
    /// back to idle so nothing is left mid-fade or mid-scale when the next spin starts.
    /// </summary>
    private void StopWinLinesLoop()
    {
        if (_winLinesLoopRoutine == null) return;

        StopCoroutine(_winLinesLoopRoutine);
        _winLinesLoopRoutine = null;

        for (int j = 0; j < SlotOverlays.Count; j++)
        {
            for (int k = 0; k < SlotOverlays[j].slotImages.Count; k++)
            {
                SlotOverlays[j].slotImages[k].gameObject.SetActive(false);
                WinFrames[j].slotImages[k].gameObject.SetActive(false);
                SetResultImageVisible(_resultImages[j].slotImages[k], true);
                //winAnimationImages[j].slotImages[k].GetComponent<ImageAnimation>().StopAnimation();
                //winAnimationImages[j].slotImages[k].gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region TweeningCode

    private void InitializeTweening(Transform slotTransform)
    {
        // Snap to top off-screen
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, 800f);

        Tween tween = slotTransform
            .DOLocalMoveY(-800f, 0.27f)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Flash);          // accelerate in, decelerate out → feels weighted
                                           // .OnStepComplete(() =>
                                           // {
                                           //     shuffleSlotImages(midTween: true);
                                           // });

        _alltweens.Add(tween);
    }

    private IEnumerator StopTweening(Transform slotTransform, int index, bool isStop)
    {
        if (!isStop)
        {
            // Wait for the current loop cycle to naturally complete
            // (the reel is at the top — off screen — at this moment)
            bool isComplete = false;
            _alltweens[index].OnStepComplete(() => isComplete = true);
            yield return new WaitUntil(() => isComplete);
        }

        _alltweens[index].Kill();

        // Start from just above the visible area, not from 600
        // This makes the landing feel like a natural continuation of the scroll
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, 350f);

        // Smooth slide down with a gentle overshoot — reduce elastic strength
        _alltweens[index] = slotTransform
            .DOLocalMoveY(0f, 0.25f)        // SlotManager landing y (use 400f for BonusManager)
            .SetEase(Ease.OutBack)            // subtle overshoot, not a full elastic bounce
            .SetSpeedBased(false);

        if (!isStop)
            yield return new WaitForSeconds(0.2f);
        else
            yield return null;
    }

    private void KillAllTweens()
    {
        if (_alltweens.Count > 0)
        {
            for (int i = 0; i < _alltweens.Count; i++)
                _alltweens[i].Kill();
            _alltweens.Clear();
        }
    }

    #endregion

    #region Helper Function

    internal int GetSymbolIndex(Sprite sprite)
    {
        for (int i = 0; i < _symbolSprites.Length; i++)
        {
            if (sprite == _symbolSprites[i])
            {
                return i;
            }
        }
        return 0;
    }

    // Row (0=top ... 3=bottom) that each payline's middle reel passes through,
    // derived from Assets/Graphics/Asstes/Paytable/Win_lines.png (line 1 = index 1).
    private static readonly int[] _middleColumnRowByLine = new int[]
    {
        -1, // unused, index 0
        0, 1, 2, 3, 0, 3, 1, 2, 2, 1,
        0, 3, 2, 1, 0, 3, 2, 1, 2, 1,
        1, 2, 1, 2, 1, 2, 2, 1, 1, 2,
        1, 2, 1, 2, 0, 3, 2, 1, 1, 2,
    };

    private int GetPerLineWinIndex(int lineIndex)
    {
        lineIndex++;
        return _middleColumnRowByLine[lineIndex];
    }

    private List<Sprite> GetAnimationSprite(int symbolID)
    {
        return symbolAnimations[symbolID].sprites;
    }

    // Visibility toggle for _resultImages that keeps the GameObject active and fades alpha
    // instead, since other code (e.g. sprite/position writes) can run on these images while hidden.
    private static void SetResultImageVisible(Image resultImage, bool visible)
    {
        Color color = resultImage.color;
        color.a = visible ? 1f : 0f;
        resultImage.color = color;
    }

    private void SetSymbolSize(Image slotImage, int symbolID, float time = 0f)
    {
        switch (symbolID)
        {
            case 0:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.28f, time);
                break;

            case 1:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.15f, time);
                break;

            case 2:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.3f, time);
                break;

            case 3:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.06f, time);
                break;

            case 4:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.1f, time);
                break;

            case 5:
                //slotImage.transform.localScale = new Vector2(0.87f, 0.87f);
                slotImage.transform.DOScale(1f, time);
                break;

            case 6:
                //slotImage.transform.localScale = new Vector2(0.87f, 0.87f);
                slotImage.transform.DOScale(1.21f, time);
                break;

            case 7:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(0.91f, time);
                break;

            case 8:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1f, time);
                break;

            case 9:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1.08f, time);
                break;

            case 10:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1.08f, time);
                break;

            case 11:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1.28f, time);
                break;

            case 12:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1f, time);
                break;

            default:
                slotImage.transform.DOScale(1f, time);
                break;
        }
    }

    private void SetAnimationSymbolSize(Image slotImage, int symbolID, float time = 0f)
    {
        switch (symbolID)
        {
            case 0:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.925f, time);
                break;

            case 1:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.15f, time);
                break;

            case 2:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.3f, time);
                break;

            case 3:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.06f, time);
                break;

            case 4:
                //slotImage.transform.localScale = new Vector2(1.15f, 1.15f);
                slotImage.transform.DOScale(1.1f, time);
                break;

            case 5:
                //slotImage.transform.localScale = new Vector2(0.87f, 0.87f);
                slotImage.transform.DOScale(1f, time);
                break;

            case 6:
                //slotImage.transform.localScale = new Vector2(0.87f, 0.87f);
                slotImage.transform.DOScale(1.21f, time);
                break;

            case 7:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(0.91f, time);
                break;

            case 8:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1f, time);
                break;

            case 9:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1.08f, time);
                break;

            case 10:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1.08f, time);
                break;

            case 11:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1.75f, time);
                break;

            case 12:
                //slotImage.transform.localScale = new Vector2(1.7f, 1.7f);
                slotImage.transform.DOScale(1f, time);
                break;

            default:
                slotImage.transform.DOScale(1f, time);
                break;
        }
    }

    // Per-symbol AnimationSpeed for the winning-line loop animation (ImageAnimation.AnimationSpeed).
    // All symbols currently share the same speed the win-loop code used to hardcode inline;
    // tune individual cases here once per-symbol timing is needed.
    private float GetAnimationSpeed(int symbolID)
    {
        switch (symbolID)
        {
            case 0:
                return 86f;
            case 1:
                return 39;
            case 2:
                return 39f;
            case 3:
                return 47f;
            case 4:
                return 47f;
            case 5:
                return 39f;
            case 6:
                return 35f;
            case 7:
                return 39f;
            case 8:
                return 69f;
            case 9:
                return 47f;
            case 10:
                return 69f;
            case 11:
                return 61f;
            case 12:
                return 31f;

            default:
                return 31f;
        }
    }

    private void ResetAllSymbol()
    {
        // foreach (var slotimage in _resultImages)
        // {
        //     foreach (var image in slotimage.slotImages)
        //     {
        //         image.transform.localScale = Vector2.one;
        //         image.GetComponent<ImageAnimation>().StopAnimation();
        //     }
        // }
        foreach (var item in SlotOverlays)
        {
            foreach (var image in item.slotImages)
            {
                image.gameObject.SetActive(false);
            }
        }
        foreach (var item in WinFrames)
        {
            foreach (var image in item.slotImages)
            {
                image.gameObject.SetActive(false);
                image.GetComponent<ImageAnimation>().StopAnimation();
            }
        }
    }

    private void ResetOverlays()
    {
        for (int j = 0; j < SlotOverlays.Count; j++)
        {
            for (int k = 0; k < SlotOverlays[j].slotImages.Count; k++)
            {
                SlotOverlays[j].slotImages[k].gameObject.SetActive(false);
                WinFrames[j].slotImages[k].gameObject.SetActive(false);
                SetResultImageVisible(_resultImages[j].slotImages[k], true);
                //winAnimationImages[j].slotImages[k].GetComponent<ImageAnimation>().StopAnimation();
                //winAnimationImages[j].slotImages[k].gameObject.SetActive(false);
            }
        }
        winAmountBigText.gameObject.SetActive(false);
        foreach (var lineWinText in lineWinTexts)
        {
            lineWinText.gameObject.SetActive(false);
        }
    }

    #endregion
}

[Serializable]
public class SlotImage
{
    public List<Image> slotImages = new List<Image>(10);
}

[Serializable]
public class AnimationSprites
{
    public List<Sprite> sprites = new List<Sprite>();
}