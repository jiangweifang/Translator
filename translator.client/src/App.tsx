import React, { Component } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import './App.css'

interface AppProps {
}

interface AppState {
    transText: string;
}

export default class App extends Component<AppProps, AppState> {

    private hubConnection: HubConnection;
    private isRuning: boolean;
    constructor(props: AppProps) {
        super(props);
        this.state = {
            transText: "",
        }
        this.isRuning = false;
        const hubUrl = "http://localhost:5033/trans";
        this.hubConnection = new HubConnectionBuilder().withUrl(hubUrl)
            .configureLogging(LogLevel.Information)
            .build();
        this.hubConnection.on("recognized", this.onRecognized.bind(this))
    }

    componentDidMount() {
        this.hubConnection.start()
    }

    componentWillUnmount() {
        this.hubConnection.stop()
    }

    onRecognized(text: string) {
        this.setState({ transText: text });
    }

    onStart() {
        if (this.isRuning) return;
        this.isRuning = true;
        this.hubConnection.invoke("Start", "zh-CN", "ja-JP", "");
    }
    onStop() {
        if (!this.isRuning) return;
        this.isRuning = false;
        this.hubConnection.invoke("Stop");
    }
    onReversal() {
        if (!this.isRuning) return;
        this.hubConnection.invoke("Reversal", "zh-CN");
    }

    render() {
        return (<div className="main">
            <div>
                <div>选择语言</div>
                <div>翻译语言</div>
            </div>
            <div>{this.state.transText}</div>
            <button onClick={this.onStart.bind(this)}>Start</button>
            <button onClick={this.onStop.bind(this)}>Stop</button>
            <button onClick={this.onReversal.bind(this)}>Reversal</button>
        </div>);
    }
}
