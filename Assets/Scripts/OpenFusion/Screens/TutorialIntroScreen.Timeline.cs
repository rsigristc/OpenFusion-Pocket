using System.Collections;
using UnityEngine;
using OpenFusion.World;
namespace OpenFusion.Screens { public sealed partial class TutorialIntroScreen {
	IEnumerator TutorialEvent()
	{
		fScreenAlpha = 0f;
		// Original hologram sparkle, rendered in the isolated character stage.
		bEventFlag = true;
		bRenderHologram = true;
		PlaySound("HologramOn");
		HologramSound = LoopSound("Machine_Running");
		EventAni(Dexter, "excellent");
		invEffect = SpawnHologramEffect();
		PlaySound("Dexter_CC01", SceneText(texts, 4));
		PlaySound("Beeping");
		yield return new WaitForSeconds(5.5f);
		PlaySound("Computress_CC01", SceneText(texts, 5));
		PlayPath("ui sound/Comm_SlideIn.wav", Sfx.SfxVolume, false);
		EventAni(Computress, "event1");
		yield return new WaitForSeconds(1f);
		PlaySound("Beeping");
		yield return new WaitForSeconds(4f);
		EventAni(Computress, "event2");
		yield return new WaitForSeconds(1.3f);
		PlaySound("Dexter_CCEnginePwr", SceneText(texts, 6));
		PlaySound("Beeping");
		yield return new WaitForSeconds(0.5f);
		DeeDee.transform.rotation = Quaternion.Euler(0f, 0f, -70f);
		PlaySound("DeeDee_Laugh_01");
		for (int i = 0; i < 200; i++)
		{
                float b = Mathf.Sin((float)i / 200f * Mathf.PI) + 0.5f;
			Transform obj = DeeDee.transform;
			Vector3 vector = vNpcPosition;
			Vector3 vector2 = new Vector3(-4.1f, 0.5f, -2f);
			Vector3 vector3 = vector + vector2;
			Vector3 vector4 = new Vector3(1.6f, 0f, 0f);
			obj.position = vector3 + vector4 * Mathf.Min(1f, b);
			yield return new WaitForSeconds(0.01f);
		}
		yield return new WaitForSeconds(0.5f);
		Transform obj2 = DeeDee.transform;
		Vector3 vector5 = vNpcPosition;
		Vector3 vector6 = new Vector3(4f, 0.5f, -2f);
		obj2.position = vector5 + vector6;
		DeeDee.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
		yield return new WaitForSeconds(1f);
		VOSubTitleClear();
		PlaySound("Engine");
		fShakeSize = 0.5f;
		bCameraShake = true;
		if (HologramSound != null)
		{
			Destroy(HologramSound);
		}
		HologramSound = LoopSound("Vibration1");
		DeeDee.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
		PlaySound("DeeDee_Laugh02");
		for (int j = 0; j < 200; j++)
		{
			float b2 = Mathf.Sin((float)j / 200f * Mathf.PI) + 0.5f;
			Transform obj3 = DeeDee.transform;
			Vector3 vector7 = vNpcPosition;
			Vector3 vector8 = new Vector3(0f, 4f, -2f);
			Vector3 vector9 = vector7 + vector8;
			Vector3 vector10 = new Vector3(0f, 0.8f, 0f);
			obj3.position = vector9 - vector10 * Mathf.Min(1f, b2);
			yield return new WaitForSeconds(0.01f);
		}
		Transform obj4 = DeeDee.transform;
		Vector3 vector11 = vNpcPosition;
		Vector3 vector12 = new Vector3(4f, 0.5f, -2f);
		obj4.position = vector11 + vector12;
		DeeDee.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
		yield return new WaitForSeconds(1f);
		PlaySound("Dexter_CCReadings", SceneText(texts, 7));
		yield return new WaitForSeconds(1.5f);
		DeeDee.transform.rotation = Quaternion.Euler(0f, 0f, 70f);
		PlaySound("DeeDee_Laugh_03");
		for (int k = 0; k < 200; k++)
		{
			float b3 = Mathf.Sin((float)k / 200f * Mathf.PI) + 0.5f;
			Transform obj5 = DeeDee.transform;
			Vector3 vector13 = vNpcPosition;
			Vector3 vector14 = new Vector3(4f, 0.5f, -2f);
			Vector3 vector15 = vector13 + vector14;
			Vector3 vector16 = new Vector3(1.4f, 0f, 0f);
			obj5.position = vector15 - vector16 * Mathf.Min(1f, b3);
			yield return new WaitForSeconds(0.01f);
		}
		Transform obj6 = DeeDee.transform;
		Vector3 vector17 = vNpcPosition;
		Vector3 vector18 = new Vector3(2f, -0.8f, -2f);
		obj6.position = vector17 + vector18;
		DeeDee.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
		EventAni(DeeDee, "walk");
		for (int l = 0; l < 200; l++)
		{
			Vector3 position = DeeDee.transform.position;
			position.x -= 0.02f;
			position.y = vNpcPosition.y - 0.8f;
			DeeDee.transform.position = position;
			DeeDee.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
			if (l == 100)
			{
				PlaySound("DeeDee_Laugh_01");
			}
			if (l == 150)
			{
				PlaySound("Dexter_CCLaunch", SceneText(texts, 8));
			}
			yield return new WaitForSeconds(0.01f);
		}
		yield return new WaitForSeconds(0.8f);
		PlaySound("DeeDee_CCButton", SceneText(texts, 9));
		yield return new WaitForSeconds(2f);
		PlaySound("Button");
		yield return new WaitForSeconds(0.5f);
		Dexter.transform.rotation = Quaternion.Euler(0f, -70f, 0f);
		if (HologramSound != null)
		{
			Destroy(HologramSound);
		}
		HologramSound = LoopSound("Vibration2");
		fShakeSize = 1.5f;
		SirenSound = LoopSound("Warning_Siren");
		PlaySound("Dexter_CCDeeDeeNo", SceneText(texts, 10));
		EventAni(Computress, "stand2");
		if (invEffect != null)
		{
			Destroy(invEffect);
		}
		EventAni(Dexter, "deedeeno");
		yield return new WaitForSeconds(1.5f);
		EventAni(Dexter, "busyrun");
		for (int m = 0; m < 100; m++)
		{
			Vector3 position2 = Dexter.transform.position;
			position2.x -= 0.02f;
			Dexter.transform.position = position2;
			yield return new WaitForSeconds(0.01f);
		}
		fShakeSize = 3f;
		yield return new WaitForSeconds(1f);
		PlaySound("Thunderous_Crash");
		yield return new WaitForSeconds(1f);
		for (int n = 0; n < 6; n++)
		{
			fScreenAlpha = (float)n / 5f;
			yield return new WaitForSeconds(0.1f);
		}
		bCameraShake = false;
		bRenderHologram = false;
		if (HologramSound != null)
		{
			Destroy(HologramSound);
		}
		if (SirenSound != null)
		{
			Destroy(SirenSound);
		}
		PlaySound("HologramOff");
		VOSubTitle = "";
		yield return new WaitForSeconds(0.5f);
		bJumpEvent = true;
		PlaySound("TimejumpText_Typed");
		yield return new WaitForSeconds(3f);
		bEventFlag = false;
	}

}}
