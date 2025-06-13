# License Manager

License Manager is a free, open-source software to manage licenses of local or remote computers

## Original Contributors

| Contributor | Description |
|--|--|
| Hotbird64 | Original developer of VLMCSD, License Manager, VLMCSD on Floppy, VLMCSD on WSL |
| Nang | Contributor of VLMCSD database
| [shiroinekotfs](https://github.com/shiroinekotfs) | Contributor of VLMCSD, License Manager, VLMCSD on Floppy, VLMCSD on WSL, VLMCSD Database |

### Use the License Manager

> This guide only applies to License Manager, version 5.5.0, built and released by TheFlightSims. If you use your own copy of License Manager, or repack by others, the guide may differ.

#### 1. The User Interface

![image](https://user-images.githubusercontent.com/99700363/225807709-d048e0cd-db62-497d-85e5-d3f8c8ada8e3.png)

`(1)` License Manager tools (stores in tabs and quick access tools)

`(2)` Connection credential and selected edition

`(3)` Verbose license information

`(4)` Verbose machine information

`(5)` Progress bar

#### 2. Install a Generic Key for the local computer

![image](https://user-images.githubusercontent.com/99700363/225810410-8003e36e-cb60-4c90-8e87-bce2586c53d3.png)

Click on **Install a Generic Key**, and select the key that you want to install. You can copy-n-paste the key, or click **Check** and then click **Install** on the Product Finder window.

> For Microsoft Visual Studio, Microsoft SQL Server, and Microsoft SCCM, you need to copy-n-paste in the product key box. License Manager cannot install these keys on your machine

#### 3. Activate your Windows or Office using KMS Server

> To activate using KMS Server, your destination computer needs a Generic Volume License Key (GVLK) installed. See [this document](https://learn.microsoft.com/en-us/windows-server/get-started/kms-client-activation-keys) to know how. Normally, the product keys stores in the License Manager that you can install are public GVLKs, so you will not need to find it on the Internet!

![image](https://user-images.githubusercontent.com/99700363/225835744-c7ae792e-adaa-4bb7-86b8-3e63a1b22183.png)

You will need to determine which server responds to your request by entering the field Override KMS Host data with the IP Address or DNS Name, or you just need to determine the KMS Domain. Don't forget to save the settings!

> Pro tip: if you use the Server edition, License Manager can verify whether the server can respond with your product activation in "Start a KMS Client"
>
> ![image](https://user-images.githubusercontent.com/99700363/225837594-879e4761-0129-4dd4-af4f-75b0d81e4e4b.png)

#### 4. Install or verify a product key

![image](https://user-images.githubusercontent.com/99700363/225839967-4452368e-b911-486e-9b85-8fc890be052a.png)

You can just simply click on Product Finder and paste your product key: you will see your product key details, including its EPID and Complete PID.

You can choose to Install it or Check the availability online.

> Note that you can only verify Microsoft Office, Microsoft Windows, and Microsoft Visual Studio. However, Microsoft Visual Studio cannot be installed - you must enter the product key manually.

#### 5. Export to Database

You can export to these specific files in License Manager:
|Export format| File extension | Description |
|--|--|--|
| VLMCSD | `.kmd` | This format used by vlmcsd, as the external (add-on) database |
| py-kms classic | `.txt` | This format used by py-kms (KMS for Python 2) |
| Generic C/C++/C# | `.txt`, `.c`, `.cpp`, `.h`, `.hpp`, `.cs` | This format helps developers to modify the vlmcsd source code |
| License Manager database, py-kms | `.xml` | This format used by License Manager
