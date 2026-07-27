using SlotSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSlot : MonoBehaviour
{
    public SlotSystem.SlotManager slotManager;
    private SlotDefinition slotDefinition;
    
    void Start()
    {
        slotDefinition = slotManager.slots[0];


       
    }

    void Update()
    {
        slotManager.Attach(slotDefinition.slotId, this.transform.gameObject);
    }

}
