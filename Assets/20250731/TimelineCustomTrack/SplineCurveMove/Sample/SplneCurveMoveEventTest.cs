using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SplneCurveMoveEventTest : MonoBehaviour
{
    
    public GameObject itemObject;
    public Transform reciverHandSlot;
    public UnityEvent onTransferStart; 
    public UnityEvent onTransferComplete;

    public void StartTransfer()
    {
        Debug.Log("开始传递物品！");
       
        if (itemObject != null)
        {
           
            itemObject.transform.SetParent(null);
        }
      
        onTransferStart.Invoke();
    }

    public void CompleteTransfer()
    {
        Debug.Log("传递物品完成！");
      
        if (itemObject != null)
        {
        
            Transform receiverHand = reciverHandSlot;
            if (receiverHand != null) itemObject.transform.SetParent(receiverHand);
            itemObject.transform.localPosition = Vector3.zero;
            itemObject.transform.localRotation = Quaternion.identity;
        }
     
        onTransferComplete.Invoke();
    }


    public void SayHello()
    {
        Debug.Log("hello!");
    }

    public void ActiveSomething(GameObject obj)
    {
        obj.SetActive(true);
    }
    public void DeactiveSomething(GameObject obj)
    {
        obj.SetActive(false);
    }
}
