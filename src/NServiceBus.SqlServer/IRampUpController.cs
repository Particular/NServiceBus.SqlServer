namespace NServiceBus.Transports.SQLServer
{
    interface IRampUpController
    {
        void Succeeded();
        void Failed();
        bool CheckHasEnoughWork();
        void RampUpIfTooMuchWork();
    }
}