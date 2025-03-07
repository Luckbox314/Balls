using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public TMPro.TMP_InputField ballsInputField;
    public string simulationSceneName = "SimulationScene";

    public void StartSimulation()
    {
        string input = ballsInputField.text;
        int ballCount = 20000;
        int.TryParse(input, out ballCount);

        GlobalSimulationSettings.numberOfBalls = ballCount;

        SceneManager.LoadScene(simulationSceneName);
        
    }
}
