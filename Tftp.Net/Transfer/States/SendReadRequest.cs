﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Channel;
using System.Net;

namespace Tftp.Net.Transfer.States
{
    class SendReadRequest : BaseState
    {
        private readonly SimpleTimer timer;

        public SendReadRequest(TftpTransfer context)
            : base(context) 
        {
            timer = new SimpleTimer(Context.Timeout);
        }

        public override void OnStateEnter()
        {
            //Send a read request to the server
            SendRequest();
        }

        private void SendRequest()
        {
            ReadRequest request = new ReadRequest(Context.Filename, Context.TransferMode, Context.Options);
            Context.GetConnection().Send(request);
            timer.Restart();
        }

        public override void OnTimer()
        {
            //Re-send the read request
            if (timer.IsTimeout())
                SendRequest();
        }

        public override void OnCommand(ITftpCommand command, EndPoint endpoint)
        {
            if (command is Data)
            {
                //The server acknowledged our read request.
                //Fix out remote endpoint
                Context.GetConnection().RemoteEndpoint = endpoint;

                //Remove any options that were not acknowledged
                Context.RemoveOptionsThatWereNotAcknowledged();

                //Switch to the receiving state...
                ITftpState nextState = new Receiving(Context);
                Context.SetState(nextState);

                //...and let it handle the data packet
                nextState.OnCommand(command, endpoint);
            }
            else if (command is OptionAcknowledgement)
            {
                //the server acknowledged our options. Confirm the final options
                Context.GetConnection().Send(new Acknowledgement(0));

                //Check which options were acknowledged
                OptionAcknowledgement ackCommand = (OptionAcknowledgement)command;
                Context.SetOptionsAcknowledged(ackCommand.Options);
            }
            else if (command is Error)
            {
                Context.SetState(new ReceivedError(Context, ((Error)command).ErrorCode, ((Error)command).Message));
            }
            else
                base.OnCommand(command, endpoint);
        }

        public override void OnCancel()
        {
            Context.SetState(new CancelledByUser(Context));
        }
    }
}