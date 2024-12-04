
using System;
using Unity.Sentis;

namespace Perrinn424.AISpeedEstimatorSystem
{
    public class AISpeedEstimator : IDisposable
    {
        private readonly Model runtimeModel;
        private readonly Worker worker;
        private readonly Tensor<float> tensorInput;

        private float evaluateSpeed;
        public float EstimatedSpeed { get; private set; }

        public AISpeedEstimator(ModelAsset modelAsset)
        {
            runtimeModel = ModelLoader.Load(modelAsset);
            worker = new Worker(runtimeModel, BackendType.CPU);
            tensorInput = new Tensor<float>(new TensorShape(1, AISpeedEstimatorInput.count)); 
        }

        /// <summary>
        /// Estimate the speed in m/s
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public float Estimate(ref AISpeedEstimatorInput input)
        {
            UpdateValues(ref input);

            worker.Schedule(tensorInput);

            Tensor<float> tensorOutput = worker.PeekOutput() as Tensor<float>;
            
            float[] results = tensorOutput?.DownloadToArray();

            evaluateSpeed = results[0];  // First value of the output tensor
            EstimatedSpeed = evaluateSpeed / 3.6f;  // Convert from km/h to m/s

            tensorOutput?.Dispose();

            return EstimatedSpeed;
        }

        private void UpdateValues(ref AISpeedEstimatorInput input) 
        {
            tensorInput[0] = input.throttle;
            tensorInput[1] = input.brake;
            tensorInput[2] = input.accelerationLateral;
            tensorInput[3] = input.accelerationLongitudinal;
            tensorInput[4] = input.accelerationVertical;
            tensorInput[5] = input.nWheelFL;
            tensorInput[6] = input.nWheelFR;
            tensorInput[7] = input.nWheelRL;
            tensorInput[8] = input.nWheelRR;
            tensorInput[9] = input.steeringAngle;
    }

        public void Dispose()
        {
            tensorInput.Dispose();
            worker.Dispose();
        }
    } 
}
