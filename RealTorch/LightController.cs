using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Common.Math;

using Light = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Light;

namespace Plugin;

public unsafe class LightController : IDisposable
{
    private const ushort TorchOrnamentId = 48;
    private const int RightHandBoneId = 65;
    private Light* lightPtr = null;

    public LightController()
    {
        S.Framework.Update += FrameworkOnUpdateEvent;

        CreateLight();
    }

    public void Dispose()
    {
        S.Framework.Update -= FrameworkOnUpdateEvent;

        DestroyLight();
    }

    private void FrameworkOnUpdateEvent(IFramework framework)
    {
        if (lightPtr == null)
            return;

        if (UpdateLightPosition())
        {
            if (!lightPtr->IsVisible)
            {
                S.Log.Debug("Showing light");
                lightPtr->IsVisible = true;
                lightPtr->UpdateRender();
            }
        }
        else
        {
            if (lightPtr->IsVisible)
            {
                S.Log.Debug("Hiding light");
                lightPtr->IsVisible = false;
                lightPtr->UpdateRender();
            }
        }
    }

    private void CreateLight()
    {
        lightPtr = Light.Create(LightShape.PointLight, "torch_light");
        lightPtr->IsVisible = false;
        lightPtr->RenderLight->Color = new Vector3(1.0f, 0.5f, 0.0f);
        lightPtr->RenderLight->Intensity = 1.0f;
        lightPtr->RenderLight->LightFlags = LightFlags.SpecularHighlights | LightFlags.CharacterShadows | LightFlags.DynamicShadows | LightFlags.ObjectShadows;
        lightPtr->RenderLight->Range = 35f;
        lightPtr->RenderLight->CharacterShadowRange = 110f;
        lightPtr->RenderLight->ShadowPlaneNear = 0.01f;
        lightPtr->RenderLight->ShadowPlaneFar = 17f;
        lightPtr->RenderLight->FalloffType = LightFalloffType.Quadratic;
        lightPtr->RenderLight->FalloffFactor = 1f;
    }

    private void DestroyLight()
    {
        if (lightPtr != null)
        {
            lightPtr->VirtualTable->CleanupRender(lightPtr);
            lightPtr->VirtualTable->Dtor(lightPtr, 0);
            lightPtr = null;
        }
    }

    private bool DrawObjectExists(GameObject* chara)
    {
        if ((nint)chara == 0 || (nint)chara->DrawObject == 0) return false;
        return chara->DrawObject->IsVisible;
    }

    private bool UpdateLightPosition()
    {
        var chara = Control.GetLocalPlayer();
        if (chara == null)
            return false;

        if (chara->OrnamentData.OrnamentId != TorchOrnamentId)
            return false;

        if (S.ObjectTable[1] == null || !DrawObjectExists((GameObject*)S.ObjectTable[1]!.Address))
            return false;

        var charaBase = (CharacterBase*)chara->DrawObject;
        if ((nint)charaBase == 0)
            return false;

        var skeleton = charaBase->Skeleton;
        if (skeleton->PartialSkeletonCount < 2)
            return false;
        var partialSkeleton = &skeleton->PartialSkeletons[0];
        var havokPose = partialSkeleton->GetHavokPose(0);
        if ((nint)havokPose == 0)
            return false;

        var basePosition = charaBase->DrawObject.Object.Position;
        var baseRotation = charaBase->DrawObject.Object.Rotation;
        var baseScale = charaBase->DrawObject.Object.Scale;
        var boneTransform = havokPose->AccessBoneModelSpace(RightHandBoneId, FFXIVClientStructs.Havok.Animation.Rig.hkaPose.PropagateOrNot.DontPropagate);

        // Player's position matrix
        var playerModelMatrix = ToMatrix(new Transform()
        {
            Position = basePosition,
            Rotation = baseRotation,
            Scale = baseScale
        });

        var boneModelPos = Vector3.Transform(new Vector3(boneTransform->Translation.X, boneTransform->Translation.Y, boneTransform->Translation.Z), playerModelMatrix);
        boneModelPos.Y += 0.18f; // target position is above the hand naturally

        lightPtr->RenderLight->Transform->Position = boneModelPos;
        lightPtr->UpdateCulling();

        return true;
    }

    public static Matrix4x4 ToMatrix(Transform transform)
    {
        System.Numerics.Matrix4x4 mat = Matrix4x4.Identity;

        mat *= System.Numerics.Matrix4x4.CreateScale(transform.Scale);

        Quaternion normalizedRotation = Quaternion.Normalize(transform.Rotation);
        mat *= System.Numerics.Matrix4x4.CreateFromQuaternion(normalizedRotation);

        mat.M41 = transform.Position.X;
        mat.M42 = transform.Position.Y;
        mat.M43 = transform.Position.Z;

        return mat;
    }
}
