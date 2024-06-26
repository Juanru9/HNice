using HNice.Service;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HNice.ViewModel
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public readonly ITcpInterceptorWorker Worker;
        public event PropertyChangedEventHandler? PropertyChanged;

        public BaseViewModel(ITcpInterceptorWorker worker) 
        {
            Worker = worker ?? throw new ArgumentNullException(nameof(worker));
        }

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public virtual async Task OnSendToClient(string packetsToSend)
        {
            if (string.IsNullOrEmpty(packetsToSend))
            {
                return;
            }
            await Worker.SendPacketToClientAsync(packetsToSend);
        }

        public async Task OnSendToServer(string packetsToSend)
        {
            if (string.IsNullOrEmpty(packetsToSend))
            {
                return;
            }
            await Worker.SendPacketToServerAsync(packetsToSend);
        }
    }
}
