import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

export class SignalRController {
    hubConnection: HubConnection;
    hubUrl: string = "http://localhost:52440/ChatTrans";
    constructor() {
        this.hubConnection = new HubConnectionBuilder().withUrl(this.hubUrl)
            .configureLogging(LogLevel.Information)
            .build(); 
    }

	public async SignalRStart() {
		try {
			if (this.hubConnection.state !== "Connected") {
				await this.hubConnection.start();
				console.log("SignalR ������.");
			} else {
				console.log("SignalR ������, ������������", this.hubConnection);
			}
		} catch (err) {
			console.log("SignalR �쳣,������������", err);
			setTimeout(
				async () => {
					await this.SignalRStart()
				}
				, 5000);
		}
	}


}