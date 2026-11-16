using UnityEngine;

public class LanderParticles : MonoBehaviour
{
    [SerializeField] private GameObject ExplosionParticleSystem;
    [SerializeField] private ParticleSystem LeftThruster;
    [SerializeField] private ParticleSystem RightThruster;
    [SerializeField] private ParticleSystem MiddleThruster;

    private Lander lander;

    private void Awake()
    {
        lander = GetComponent<Lander>();
        lander.OnCrash += Lander_OnCrash;
        lander.OnUp += Lander_OnUp;
        lander.OnBeforeForce += Lander_OnBeforeForce;
        lander.OnLeft += Lander_OnLeft;
        lander.OnRight += Lander_OnRight;

    }

    private void Lander_OnRight(object sender, System.EventArgs e)
    {
        SetEnableThrusterParticle(LeftThruster, true); 
    }

    private void Lander_OnLeft(object sender, System.EventArgs e)
    {
        SetEnableThrusterParticle(RightThruster, true);
    }

    private void Lander_OnBeforeForce(object sender, System.EventArgs e)
    {
        SetEnableThrusterParticle(LeftThruster, false);
        SetEnableThrusterParticle(RightThruster, false);
        SetEnableThrusterParticle(MiddleThruster, false);
    }

    private void Lander_OnUp(object sender, System.EventArgs e)
    {
        SetEnableThrusterParticle(MiddleThruster, true);
    }

    private void Lander_OnCrash(object sender, System.EventArgs e)
    {
        Instantiate(ExplosionParticleSystem, transform.position, Quaternion.identity);
        gameObject.SetActive(false);
    }
    private void SetEnableThrusterParticle(ParticleSystem particleSystem, bool enabled)
    {
        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.enabled = enabled;
    }
}
