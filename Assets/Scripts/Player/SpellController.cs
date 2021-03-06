﻿using CustomInput;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellController : MonoBehaviour {

    public Transform spellContainer;
    public List<ISpell> acquiredSpells = new List<ISpell>();
    public ISpell currentSpell;
    private int spellIndex = 0;
    private PlayerInput playerInput;
    private FirstPersonDrifter fpsController;

    public Vector3 targetedPoint;
    private Vector3 centerScreen;

    private float heatLevel = 0f;
    public float maxHeat = 100f;
    public float overHeatLevel = 75f;
    public float recoveryTime = 0.5f;
    public float ventRate = 5f;
    private float recoveryTimer;
    private float switchBuffer = 0.25f;
    private float switchTimer;
    private bool isSwitching = false;
    private bool castTimerElapsed = true;
    public bool isCasting = false;
    public bool isVenting = false;
    public delegate void UpdateUI(float newHeatLevel);
    public UpdateUI OnHeatChange = delegate { };

    public LayerMask raycastMask;

    // init
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        fpsController = GetComponent<FirstPersonDrifter>();
        foreach(Transform child in spellContainer)
        {
            ISpell spell = child.GetComponent<ISpell>();
            // Ignore transforms that don't have spells on them, but we will warn
            if (spell != null)
            {
                acquiredSpells.Add(child.GetComponent<ISpell>());
                child.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning(child.name + " has no spell attached!");
            }

            
        }
        
        currentSpell = acquiredSpells[0];
        currentSpell.Equip(this);
    }



    private void Start()
    {
        OnHeatChange(heatLevel);
    }

    // Update is called once per frame
    void Update () {
        centerScreen = new Vector3(Screen.height / 2, Screen.width / 2, 0);
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100, raycastMask)){
            targetedPoint = hit.point;
            Debug.DrawLine(Camera.main.transform.position, targetedPoint, Color.red);
        }
        else
        {
            targetedPoint = ray.GetPoint(100);
            Debug.DrawLine(Camera.main.transform.position, targetedPoint, Color.blue);
        }
        
        if (playerInput.GetButtonInput(PlayerInput.SWITCH_SPELL_FORWARD) && !isSwitching)
        {
            CycleSpellForward();
        }

        if (playerInput.GetButtonInput(PlayerInput.SWITCH_SPELL_BACK) && !isSwitching)
        {
            CycleSpellBackward();
        }

        if (isSwitching)
        {
            switchTimer += Time.deltaTime;
            if(switchTimer > switchBuffer)
            {
                isSwitching = false;
                switchTimer = 0;
            }

        }


        HandleVenting();
        HandleSpells();

        // Allow sprint to interrupt casting and venting
        if (playerInput.GetButtonInput(PlayerInput.SPRINT_BUTTON))
        {
            fpsController.enableRunning = true;
            isVenting = false;
            isCasting = false;
        }
        		
	}

    void HandleSpells()
    {
        if (currentSpell.IsLooping())
        {

            if (playerInput.GetButtonInput(PlayerInput.CAST_BUTTON) && !playerInput.GetButtonInput(PlayerInput.SPRINT_BUTTON) && !isVenting)
            {
                if (!isCasting)
                {
                    currentSpell.Cast(this);
                    fpsController.enableRunning = false;
                    isCasting = true;
                }
            }
            else
            {
                currentSpell.Stop(this);
                fpsController.enableRunning = true;
                isCasting = false;
            }

            if (isCasting)
            {
                AddHeat(currentSpell.GetHeat() * Time.deltaTime);
            }
        }
        else
        {
            // reset isCasting on next frame so the animator can catch it in the last frame
            isCasting = false;
            if (playerInput.GetButtonInput(PlayerInput.CAST_BUTTON_DOWN) && !playerInput.GetButtonInput(PlayerInput.SPRINT_BUTTON) && castTimerElapsed && !isVenting)
            {
                castTimerElapsed = false;
                currentSpell.Cast(this);
                AddHeat(currentSpell.GetHeat());
                isCasting = true;
            }
        }


        if (!currentSpell.IsLooping() && !castTimerElapsed)
        {
            recoveryTimer += Time.deltaTime;
            if (recoveryTimer >= recoveryTime)
            {
                castTimerElapsed = true;
                recoveryTimer = 0;
            }
        }
    }

    void HandleVenting()
    {
        if (playerInput.GetButtonInput(PlayerInput.VENT_BUTTON) && !playerInput.GetButtonInput(PlayerInput.SPRINT_BUTTON))
        {
            currentSpell.Stop(this);
            isCasting = false;
            fpsController.enableRunning = false;
            isVenting = true;
        }
        else
        {
            fpsController.enableRunning = true;
            isVenting = false;
        }

        if (isVenting)
        {
            VentHeat(ventRate * Time.deltaTime);
        }
    }


    void CycleSpellForward()
    {
        if (!isCasting && !isSwitching)
        {
            isSwitching = true;
            currentSpell.UnEquip(this);
            int currentIndex = acquiredSpells.IndexOf(currentSpell);
            if (currentIndex == acquiredSpells.Count - 1)
            {
                spellIndex = 0;
                currentSpell = acquiredSpells[0];
            }
            else
            {
                spellIndex++;
                currentSpell = acquiredSpells[spellIndex];
            }
            currentSpell.Equip(this);
        }          
    }

    void CycleSpellBackward()
    {
        if (!isCasting && !isSwitching)
        {
            isSwitching = true;
            currentSpell.UnEquip(this);
            int currentIndex = acquiredSpells.IndexOf(currentSpell);
            if (currentIndex == 0)
            {
                spellIndex = acquiredSpells.Count - 1;
                currentSpell = acquiredSpells[acquiredSpells.Count - 1];
            }
            else
            {
                spellIndex--;
                currentSpell = acquiredSpells[spellIndex];
            }
            currentSpell.Equip(this);
        }
    }

    public float GetHeatLevel()
    {
        return heatLevel;
    }

    public void AddHeat(float heat)
    {
        if(heatLevel + heat > overHeatLevel)
        {
            GetComponent<PlayerHealth>().Damage(heat);
        }

        if(heatLevel + heat <= maxHeat)
        {
            heatLevel += heat;
        }
        else
        {
            heatLevel = maxHeat;
        }
        OnHeatChange(heatLevel);

    }

    public void VentHeat(float heat)
    {
        if (heatLevel > 0)
        {
            if (heatLevel - heat <= 0)
            {
                heatLevel = 0;
            }
            else
            {
                heatLevel -= heat;
            }
            OnHeatChange(heatLevel);
        }
    }
}
