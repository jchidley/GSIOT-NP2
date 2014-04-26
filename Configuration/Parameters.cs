using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Configuration
{
    public class Parameters
    {
        // parameters for I/O ports specific to the used hardware
        public static Cpu.Pin LedPin = Pins.ONBOARD_LED;
        public static Cpu.Pin ButtonPin = Pins.ONBOARD_BTN;
        public static Cpu.Pin LowPin = Pins.GPIO_PIN_A0;        // dummy, not needed on Gadgeteer, don't connect anything to socket 5
        public static Cpu.Pin HighPin = Pins.GPIO_PIN_A2;       // dummy, not needed on Gadgeteer, don't connect anything to socket 5
        public static Cpu.AnalogChannel AnalogPin = AnalogChannels.ANALOG_PIN_A1;
        // connect an analog input to pin 3 of socket 6, e.g., a GHI Potentiometer module

        // parameters for Xively samples
        // check at https://xively.com/feeds/1978138106
        public static string ApiKey = "FSROGLGYAzdjj8rzGT9PfR3GqVBItuj9HgiZ1bQIQy64LqaD";
        public static string FeedId = "1978138106";

        // parameters for server samples
        public static string RelayDomain = "gsiot-8a3m-5w8t";
        public static string RelaySecretKey = "JNe22aDLBeSmTDbc1iOZMwN81rwA3OuGUS++wsBl";
    }
}