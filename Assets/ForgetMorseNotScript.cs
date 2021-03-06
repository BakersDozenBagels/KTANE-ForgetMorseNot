using KeepCoding;
using KModkit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class ForgetMorseNotScript : MonoBehaviour
{
    [SerializeField]
    private Renderer _screen;
    [SerializeField]
    private Material _offMat, _onMat;
    [SerializeField]
    private KMBossModule _boss;
    [SerializeField]
    private KMAudio _audio;
    [SerializeField]
    private KMBombInfo _info;
    [SerializeField]
    private TextMesh _text;
    [SerializeField]
    private KMBombModule _module;

    private string[] _ignored;
    private static readonly string[] PROSIGNS = new string[] { ".-. .--. -", "--.- - --.-", "--.- - ..." };
    private int _currentStage, _submissionStage, _currentlyAskedStage, _numberInput, _id = ++_idc, _nonIgnoredSolves;
    private float _lastHeld, _lastReleased = -1f, _playSpeed = 9f, _lastStrike, _checkForSolves;
    private string _currentSubmission = string.Empty, _expectedSubmission = string.Empty;
    private bool _solveOnPress, _autoSolving, _isSolved, _suddenChange;
    private List<string> _transmissions = new List<string>(), _rememberedStages = new List<string>();
    private KMAudio.KMAudioRef _audioRef;
    private static int _idc;

    private void Start()
    {
        _ignored = _boss.GetIgnoredModules(_module, new string[] {
            "Forget Morse Not"
        });
#if UNITY_EDITOR
        _submissionStage = 2;
#else
        _submissionStage = _info.GetSolvableModuleNames().Where(n => !_ignored.Contains(n) && n != "Forget Morse Not").Count();
#endif
        if(_submissionStage == 0)
        {
            _solveOnPress = true;
        }
        GetComponent<KMSelectable>().Children[0].Assign(onInteract: Press, onInteractEnded: Release);
        _transmissions.Add(null);
        _rememberedStages.Add(null);

        _info.OnBombExploded += Off;
        _info.OnBombSolved += Off;
    }

    private void Press()
    {
        On();
        if(_isSolved)
            return;
        if(_solveOnPress)
        {
            Solve("Solving because no non-ignored modules were found.");
            return;
        }
        _lastHeld = Time.time;
        _lastReleased = -1f;
    }

    private void On()
    {
        _screen.material = _onMat;
        if(_audioRef != null && _audioRef.StopSound != null)
            _audioRef.StopSound();
        _audioRef = _audio.PlaySoundAtTransformWithRef("Beep", transform);
    }

    private void Release()
    {
        Off();
        if(_solveOnPress || _isSolved || Time.time - _lastStrike < 3f)
            return;
        if(Time.time - _lastHeld < 0.5f)
            _currentSubmission += ".";
        else
            _currentSubmission += "-";
        _lastReleased = Time.time;
    }

    private void Off()
    {
        _screen.material = _offMat;
        _audioRef.StopSound();
    }

    private void Update()
    {
        if(_lastReleased != -1f && Time.time - _lastReleased > 1f)
        {
            Submit();
            _lastReleased = -1f;
        }

        if(_checkForSolves < 1f)
            _checkForSolves += Time.deltaTime;
        else
        {
            _checkForSolves = 0f;
            int _solves = _info.GetSolvedModuleNames().Where(n => !_ignored.Contains(n)).Count();
            while(_solves > _nonIgnoredSolves)
            {
                OnModuleSolved();
                _nonIgnoredSolves++;
            }
        }
    }

    private void Submit()
    {
        if(_expectedSubmission == _currentSubmission)
        {
            Log("You submitted \"{0}\". Correct.".Form(_currentSubmission));
            _suddenChange = true;
            _expectedSubmission = string.Empty;
            _currentSubmission = string.Empty;
            if(_currentlyAskedStage != 0)
            {
                _numberInput++;
                _currentlyAskedStage = 0;
                if(_numberInput < Random.Range(Mathf.FloorToInt(Mathf.Sqrt(_submissionStage)), _submissionStage / 2))
                    StartCoroutine(ShowStage());
                else
                {
                    _audio.PlaySoundAtTransform("Solved", transform);
                    Solve("Congratulations!");
                }
            }
            else
                _audio.PlaySoundAtTransform("Correct", transform);
            return;
        }
        if(PROSIGNS[0] == _currentSubmission)
        {
            StartCoroutine(ShowStage(true));
            _currentSubmission = string.Empty;
            _suddenChange = true;
            return;
        }
        if(PROSIGNS[1] == _currentSubmission)
        {
            _playSpeed += 3f;
            _playSpeed = Mathf.Min(_playSpeed, 15f);
            _currentSubmission = string.Empty;
            _audio.PlaySoundAtTransform("Correct", transform);
            _suddenChange = true;
            return;
        }
        if(PROSIGNS[2] == _currentSubmission)
        {
            _playSpeed -= 3f;
            _playSpeed = Mathf.Max(_playSpeed, 6f);
            _currentSubmission = string.Empty;
            _audio.PlaySoundAtTransform("Correct", transform);
            _suddenChange = true;
            return;
        }
        _currentSubmission += " ";
        if(!_expectedSubmission.StartsWith(_currentSubmission) && !PROSIGNS.Any(s => s.StartsWith(_currentSubmission)))
        {
            if(_currentlyAskedStage != 0)
            {
                _suddenChange = true;
                if(!_autoSolving)
                {
                    Strike("You submitted \"{0}\". That isn't correct. Strike!".Form(_currentSubmission));
                    _lastStrike = Time.time;
                    _currentSubmission = string.Empty;
                    _expectedSubmission = string.Empty;
                }
                _numberInput++;
                _currentlyAskedStage = 0;
                if(_numberInput < Random.Range(Mathf.FloorToInt(Mathf.Sqrt(_submissionStage)), _submissionStage / 2))
                    StartCoroutine(ShowStage());
                else
                {
                    _audio.PlaySoundAtTransform("Solved", transform);
                    Solve("Congratulations!");
                }
                return;
            }
            if(!_autoSolving)
            {
                _suddenChange = true;
                Strike("You submitted \"{0}\". That isn't correct. Strike!".Form(_currentSubmission));
                _lastStrike = Time.time;
            }
            _currentSubmission = string.Empty;
            return;
        }
    }

    private void OnModuleSolved()
    {
        _currentStage++;
        _text.text = _currentStage.ToString();
        StartCoroutine(ShowStage());
    }

    private IEnumerator ShowStage(bool isRepeat = false)
    {
        string transmission = string.Empty;
        if(_currentStage >= _submissionStage)
        {
            _text.text = "?";
            List<int> nonNullStages = Enumerable.Range(0, _rememberedStages.Count).Where(i => _rememberedStages[i] != null).ToList();
            if(nonNullStages.Count == 0)
            {
                transmission = "..--.. ..--..";
                if(!isRepeat)
                    _expectedSubmission = ".";
                _numberInput = 999;
                _currentlyAskedStage = -1;
                Log("Transmission received: \"{0}\".", transmission);
                Log("Expected input: \"{0}\"", _expectedSubmission);
            }
            else
            {
                if(!isRepeat)
                {
                    if(_expectedSubmission != string.Empty && !_autoSolving)
                    {
                        Strike("You failed to submit \"{0}\". Strike!".Form(_expectedSubmission));
                        _lastStrike = Time.time;
                    }
                    if(_currentlyAskedStage == 0)
                        _currentlyAskedStage = nonNullStages[Random.Range(0, nonNullStages.Count)];
                }
                transmission = "..--.. " + Morsify(_currentlyAskedStage);
                _expectedSubmission = _rememberedStages[_currentlyAskedStage];
                Log("Transmission received: \"{0}\".", transmission);
                Log("Expected input: \"{0}\"", _expectedSubmission);
            }
        }
        else
        {
            _currentSubmission = string.Empty;
            while(_transmissions.Count() < _currentStage + 1)
            {
                if(_expectedSubmission != string.Empty && !_autoSolving)
                {
                    Strike("You failed to submit \"{0}\". Strike!".Form(_expectedSubmission));
                    _lastStrike = Time.time;
                }
                _transmissions.Add(RandomStage());
                Log("Transmission received: \"{0}\".", _transmissions.Last());
                if(_expectedSubmission == string.Empty)
                    Log("No input expected.");
                else
                    Log("Expected input: \"{0}\"", _expectedSubmission);
            }
            transmission = _transmissions[_currentStage];
            if(transmission == null)
                transmission = string.Empty;
        }

        foreach(char c in transmission)
        {
            if(c == '.')
            {
                On();
                yield return new WaitForSecondsRealtime(2f / _playSpeed);
                Off();
                yield return new WaitForSecondsRealtime(2f / _playSpeed);
            }

            if(c == '-')
            {
                On();
                yield return new WaitForSecondsRealtime(6f / _playSpeed);
                Off();
                yield return new WaitForSecondsRealtime(2f / _playSpeed);
            }

            if(c == ' ')
            {
                Off();
                yield return new WaitForSecondsRealtime(4f / _playSpeed);
            }
        }
    }

    private string RandomStage()
    {
        if(Random.Range(0, 3) == 0)
        {
            if(Random.Range(0, 2) == 0)
            {
                List<int> stages = Enumerable.Range(0, _rememberedStages.Count).Where(i => _rememberedStages[i] != null).ToList();
                if(stages.Count == 0)
                    goto noValidStages;
                int stage = stages.PickRandom();
                string stageTransmission = Morsify(stage);
                _expectedSubmission = _rememberedStages[stage];
                _rememberedStages.Add(null);
                return stageTransmission;
            }
            noValidStages:
            string transmission = "-.-. --.-|.--.|-..-|--.-|-.-.|.-.|-.-".Split("|").PickRandom();
            _expectedSubmission = string.Empty;
            switch(transmission)
            {
                case "-.-. --.-":
                    _expectedSubmission = ".-.";
                    break;
                case ".--.":
                    _expectedSubmission = Morsify(KMBombInfoExtensions.GetPorts(_info).Count());
                    break;
                case "-..-":
                    _expectedSubmission = IsPrime(_info.GetModuleNames().Count) ? ".-" : "-.";
                    break;
                case "...-":
                    _expectedSubmission = KMBombInfoExtensions.GetSerialNumberLetters(_info).Any(c => "AEIOU".Contains(c)) ? ".-" : "-.";
                    break;
                case "-...":
                    _expectedSubmission = Morsify(KMBombInfoExtensions.GetBatteryCount(_info));
                    break;
                case ".-.":
                    _expectedSubmission = Morsify(KMBombInfoExtensions.GetIndicators(_info).Where(s => s.Contains("R")).Count());
                    break;
                case "-.-":
                    _expectedSubmission = (transform.root.GetComponentInChildren<KMWidget>() || _info.GetSolvableModuleNames().Contains("Widgetry")) ? ".-" : "-.";
                    break;
            }
            _rememberedStages.Add(null);
            return transmission;
        }
        else
        {
            _expectedSubmission = string.Empty;
            string transmission = ".- -.-. -.. . ..-. --. .... .. .--- .-.. -- -. --- ... - ..- .-- -.-- --..".Split(" ").PickRandom();
            _rememberedStages.Add(transmission);
            return transmission;
        }
    }

    private bool IsPrime(int count)
    {
        return new int[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313, 317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397, 401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499, 503, 509, 521, 523, 541, 547, 557, 563, 569, 571, 577, 587, 593, 599, 601, 607, 613, 617, 619, 631, 641, 643, 647, 653, 659, 661, 673, 677, 683, 691, 701, 709, 719, 727, 733, 739, 743, 751, 757, 761, 769, 773, 787, 797, 809, 811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941, 947, 953, 967, 971, 977, 983, 991, 997 }
        .Contains(count);
    }

    private string Morsify(int v)
    {
        return v.ToString().Select(c =>
        {
            switch(c)
            {
                case '0':
                    return "-----";
                case '1':
                    return ".----";
                case '2':
                    return "..---";
                case '3':
                    return "...--";
                case '4':
                    return "....-";
                case '5':
                    return ".....";
                case '6':
                    return "-....";
                case '7':
                    return "--...";
                case '8':
                    return "---..";
                case '9':
                    return "----.";
                default:
                    return "";
            }
        }).Join(" ");
    }

    private void Solve(string message)
    {
        Log(message);
        _module.HandlePass();
    }

    private void Strike(string message)
    {
        Log(message);
        _module.HandleStrike();
    }

    private void Log(string message, object arg = null)
    {
        Debug.LogFormat("[Forget Morse Not #" + _id + "] " + message, arg);
    }

#pragma warning disable 414
    private const string TwitchHelpMessage = "\"!{0} - -..-\" to transmit those characters.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if((m = Regex.Match(command, @"^(?:press|transmit|tx|push|send|submit)?\s*((-|.|\s+)+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            yield return null;
            _suddenChange = false;
            KMSelectable button = GetComponent<KMSelectable>().Children[0];
            foreach(char c in m.Groups[1].Value.Trim())
            {
                if(_suddenChange)
                {
                    yield return "sendtochat Input was stopped due to a sudden change in the module's state.";
                    yield break;
                }
                if(c == '-')
                {
                    button.OnInteract();
                    yield return new WaitForSeconds(0.75f);
                    button.OnInteractEnded();
                    yield return new WaitForSeconds(0.25f);
                }
                else if(c == '.')
                {
                    button.OnInteract();
                    yield return new WaitForSeconds(0.25f);
                    button.OnInteractEnded();
                    yield return new WaitForSeconds(0.25f);
                }
                else
                {
                    yield return new WaitForSeconds(1f);
                }
            }
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        _autoSolving = true;
        _playSpeed = 30f;
        while(!_isSolved)
        {
            while(_expectedSubmission == "")
            {
                yield return true;
            }
            float time = Time.time;
            while(Time.time - time < 5f)
                yield return true;
            foreach(object e in ProcessTwitchCommand(_expectedSubmission).AsEnumerable())
                yield return e;
        }
    }
}
