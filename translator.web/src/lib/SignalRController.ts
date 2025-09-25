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
				console.log("SignalR 已连接.");
			} else {
				console.log("SignalR 已连接, 请勿重新连接", this.hubConnection);
			}
		} catch (err) {
			console.log("SignalR 异常,正在重新连接", err);
			setTimeout(
				async () => {
					await this.SignalRStart()
				}
				, 5000);
		}
	}


}