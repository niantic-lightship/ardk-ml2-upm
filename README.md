## Name
ardk-ml2-upm

## Description
The ARDK Magic Leap UPM is a Unity package needed in order to use the ARDK on the Magic Leap 2 platform. This file can be be brought into your project by using the Unity Package Manager. You can install the UPM package via a git URL, or from a Tarball (`.tgz`) file.

### Installing the ARDK Magic Leap Plugin with a URL
1. In your Unity project open the **Package Manager** by selecting **Window > Package Manager**. 
    - From the plus menu on the Package Manager tab, select **Add package from git URL...**
    - Enter `https://github.com/niantic-lightship/ardk-ml2-upm.git`. 
    - Click **Yes** to activate the new Input System Package for AR Foundation 5.0 (if prompted)

### Installing the ARDK Magic Leap Plugin from Tarball
1. This package is dependent on the **Niantic Lightship Plugin**. To install it, please go to: https://github.com/niantic-lightship/ardk-upm 
2. Download the plugin packages (`.tgz`) from the latest release
    - [ardk-upm](https://github.com/niantic-lightship/ardk-ml2-upm/releases/latest)
3. In your Unity project open the **Package Manager** by selecting **Window > Package Manager**. 
    - From the plus menu on the Package Manager tab, select **Add package from tarball...**
    - Navigate to where you downloaded the package, select the `.tgz` file you downloaded, and press **Open**. This will install the package in your project's **Packages** folder as the **Niantic Lightship Magic Leap Plugin** folder. 
    - Click **Yes** to activate the new Input System Package for AR Foundation 5.0 (if prompted). 

## Support
For any other issues, [contact us](https://lightship.dev/docs/ardk/contact_us/) on Discord or the Lightship forums! Before reaching out, open the Console Log by holding three touches on your device's screen for three seconds, then take a screenshot and post it along with a description of your issue.
