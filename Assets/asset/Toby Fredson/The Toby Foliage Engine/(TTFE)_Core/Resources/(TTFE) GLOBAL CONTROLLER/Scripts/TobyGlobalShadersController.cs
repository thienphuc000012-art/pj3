using UnityEngine;
using UnityEngine.SceneManagement;

namespace TobyFredson
{
	[ExecuteInEditMode]
	public class TobyGlobalShadersController : MonoBehaviour
	{
		#region Inspector Fields
		[SerializeField] private TobyWindType windType;
		[SerializeField] [Range(0f, 3f)] private float windStrength;
		[SerializeField] [Range(1f, 3f)] private float windSpeed;
		[SerializeField] [Range(0f, 1f)] private float windMotion;
		[SerializeField] [Range(-2f, 2f)] private float season;
		[SerializeField] private bool _showWindDirectionGizmo; // Toggle for wind direction gizmo
		#endregion

		#region Private Fields
		private TobyShaderValuesModel lastInputValues;
		#endregion

		#region Unity Callbacks
		protected void Awake()
		{
			// Ensure only one controller exists
			TobyGlobalShadersController[] controllers = FindObjectsOfType<TobyGlobalShadersController>(true);
			if (controllers.Length > 1)
			{
				Debug.LogWarning($"Multiple controllers found in scene {SceneManager.GetActiveScene().name}. Destroying duplicates.");
				for (int i = 1; i < controllers.Length; i++)
				{
					Destroy(controllers[i].gameObject);
				}
			}
		}

        private void OnEnable() { SetGlobalShaderProperties(); }
        private void OnValidate() { if (isActiveAndEnabled) SetGlobalShaderProperties(); }
        private void OnDisable()
        {
            // Reset ownership when a scene/controller goes away; another active controller can take over.
            foreach (var controller in FindObjectsByType<TobyGlobalShadersController>(FindObjectsSortMode.None))
                if (controller != this && controller.isActiveAndEnabled) { controller.SetGlobalShaderProperties(); return; }
            Shader.SetGlobalFloat("_TTFEControllerWindActive", 0f);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWindOverride() { Shader.SetGlobalFloat("_TTFEControllerWindActive", 0f); }

		protected void Start()
		{
			SetGlobalShaderProperties();
		}

		protected void Update()
		{
			// Restrict rotation to Y-axis for wind direction
			transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);

			var newValues = GetNewValues();
			if (!newValues.Equals(lastInputValues))
			{
				lastInputValues = newValues;
				SetGlobalShaderProperties();
			}
		}

		private void OnDrawGizmos()
		{
			if (_showWindDirectionGizmo)
			{
				Gizmos.color = Color.green;
				Vector3 windDirection = transform.forward;
				Vector3 position = transform.position;
				float gizmoLength = 3f;
				Vector3 endPoint = position + windDirection * gizmoLength;

				Gizmos.DrawLine(position, endPoint);
				float arrowSize = 0.5f;
				Vector3 arrowPoint1 = endPoint - windDirection * arrowSize + Quaternion.Euler(0, 30, 0) * windDirection * arrowSize;
				Vector3 arrowPoint2 = endPoint - windDirection * arrowSize + Quaternion.Euler(0, -30, 0) * windDirection * arrowSize;
				Gizmos.DrawLine(endPoint, arrowPoint1);
				Gizmos.DrawLine(endPoint, arrowPoint2);
			}
		}
		#endregion

		#region Private Methods
		private void SetGlobalShaderProperties()
		{
			Shader.SetGlobalFloat("_TTFEControllerWindStrength", windType == TobyWindType.WindOff ? 0f : Mathf.Max(0f, windStrength));
            Shader.SetGlobalFloat("_TTFEControllerWindActive", 1f);
            Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_SEASON, season);

			if (windType == TobyWindType.WindOff)
			{
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_STRENGTH, 0f);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_SPEED, 0f);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_MOTION, 0f);
				Shader.SetGlobalVector(TobyConstants.SHADER_VAR_VECTOR_WIND_DIRECTION, Vector4.zero);
			}
			else
			{
				// GentleBreeze → use the sliders
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_STRENGTH, windStrength);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_SPEED, windSpeed);
				Shader.SetGlobalFloat(TobyConstants.SHADER_VAR_FLOAT_WIND_MOTION, windMotion);
				Shader.SetGlobalVector(TobyConstants.SHADER_VAR_VECTOR_WIND_DIRECTION, 
					new Vector4(transform.forward.x, transform.forward.y, transform.forward.z, 0f));
			}

			// Wind type keywords – only GentleBreeze and WindOff remain
			Shader.DisableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_GENTLEBREEZE);
			Shader.DisableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_WINDOFF);

			switch (windType)
			{
			case TobyWindType.GentleBreeze:
				Shader.EnableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_GENTLEBREEZE);
				break;
			case TobyWindType.WindOff:
				Shader.EnableKeyword(TobyConstants.SHADER_WIND_TYPE_VALUE_WINDOFF);
				break;
			}
		}

		private TobyShaderValuesModel GetNewValues()
		{
			return new TobyShaderValuesModel
			{
				season = season,
				windType = windType,
				windStrength = windStrength,
				windSpeed = windSpeed,
				windMotion = windMotion,
				windDirection = transform.forward
			};
		}
		#endregion
	}
}